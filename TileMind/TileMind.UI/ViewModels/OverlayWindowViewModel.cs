using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using OpenCvSharp;
using System.Windows;
using System.Windows.Media;
using CvPoint = OpenCvSharp.Point;
using CvRect = OpenCvSharp.Rect;
using TileMind.Common.Config;
using TileMind.Common.Helpers;
using TileMind.Common.Models;
using TileMind.Core.Services;
using TileMind.UI.Overlay;
using TileMind.Vision.ScreenCapture;
using TileMind.UI.Overlay.OverlayBase;
using TileMind.UI.Overlay.OverlayBase.DrawingCommand;
using RectangleF = System.Drawing.RectangleF;

namespace TileMind.UI.ViewModels;

public partial class OverlayWindowViewModel : ViewModel
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ScreenCaptureOptions _screenOpts;
    private readonly OverlayOptions _overlayOpts;
    private readonly MonitorService _monitorService;
    private readonly MahjongTileCommandGenerator _commandGenerator = new();
    private readonly ILogger<OverlayWindowViewModel> _logger;

    private CancellationTokenSource? _cts;
    private IServiceScope? _pipelineScope;
    private Task? _pipelineTask;

    /// <summary>流水线是否正在运行。</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartPipelineCommand))]
    private bool _isPipelineRunning;

    /// <summary>覆盖层绘制项集合（绑定到 XAML ItemsSource）。</summary>
    public ObservableCollection<DrawingInfo> OverlayItems { get; } = new();

    /// <summary>当前已添加的区域项（用于开关切换）。</summary>
    private readonly List<ScreenRegionDrawingInfo> _regionItems = new();

    /// <summary>覆盖层功能开关配置。</summary>
    public OverlayOptions OverlayOptions => _overlayOpts;

    /// <summary>截取显示器的物理像素边界。</summary>
    private RectangleF _captureBounds;

    /// <summary>覆盖层显示器的物理像素边界。</summary>
    private RectangleF _overlayPhysicalBounds;

    /// <summary>截取与覆盖不在同一显示器时需要坐标映射。</summary>
    private bool _needsCoordMapping;

    public OverlayWindowViewModel(
        FrameStateHub hub,
        ScreenCaptureOptions screenOpts,
        OverlayOptions overlayOpts,
        MonitorService monitorService,
        IServiceProvider serviceProvider,
        ILogger<OverlayWindowViewModel> logger)
    {
        _screenOpts = screenOpts;
        _overlayOpts = overlayOpts;
        _monitorService = monitorService;
        _serviceProvider = serviceProvider;
        _logger = logger;

        // 坐标映射：截取屏 → 覆盖屏
        var captureMonitor = monitorService.FindByOutputIndex(screenOpts.OutputIndex);
        var overlayMonitor = monitorService.FindByOutputIndex(overlayOpts.OutputIndex);
        if (captureMonitor != null)
            _captureBounds = captureMonitor.Bounds;
        if (overlayMonitor != null)
            _overlayPhysicalBounds = overlayMonitor.Bounds;

        _needsCoordMapping = screenOpts.OutputIndex != overlayOpts.OutputIndex
                             && captureMonitor != null && overlayMonitor != null;
        if (_needsCoordMapping)
            _logger.LogInformation("跨屏坐标映射已启用: 截取屏 Out{CaptureOut} → 覆盖屏 Out{OverlayOut}",
                screenOpts.OutputIndex, overlayOpts.OutputIndex);

        // 区域数据是静态的，初始化时绘制
        if (_overlayOpts.ShowScreenRegions)
            DrawScreenRegions();

        hub.FrameAnalyzed += OnFrameAnalyzed;
        hub.FrameTiming += OnFrameTiming;
        hub.TileAnalysisReady += OnTileAnalysisReady;
    }

    private bool CanStartPipeline() => !IsPipelineRunning;

    [RelayCommand(CanExecute = nameof(CanStartPipeline))]
    private void StartPipeline()
    {
        if (_pipelineTask != null) return;

        IsPipelineRunning = true;
        _cts = new CancellationTokenSource();
        _pipelineScope = _serviceProvider.CreateScope();
        var pipeline = _pipelineScope.ServiceProvider.GetRequiredService<GamePipelineService>();

        _pipelineTask = Task.Run(async () =>
        {
            _logger.LogInformation("流水线开始。");
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    pipeline.ProcessFrame();
                    await Task.Delay(500, _cts.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "流水线异常。");
            }
            finally
            {
                _logger.LogInformation("流水线停止。");
            }
        }, _cts.Token);
    }

    [RelayCommand]
    private void StopPipeline()
    {
        _cts?.Cancel();
        _pipelineTask?.Wait(TimeSpan.FromSeconds(5));
        _pipelineTask = null;
        _pipelineScope?.Dispose();
        _pipelineScope = null;
        IsPipelineRunning = false;
    }

    /// <summary>
    /// 重定位：重新查找游戏窗口 → Ratio 解析绝对坐标。失败则保留旧坐标。
    /// </summary>
    [RelayCommand]
    private void RelocateCoordinates()
    {
        var clientRect = WindowFinderHelper.FindClientRect(_screenOpts.GameProcessName ?? "");
        RectangleF refRect;
        bool usingWindow;
        if (clientRect.HasValue)
        {
            refRect = clientRect.Value;
            usingWindow = true;
        }
        else
        {
            var monitor = _monitorService.FindByOutputIndex(_screenOpts.OutputIndex);
            refRect = monitor?.Bounds ?? new RectangleF();
            usingWindow = false;
        }

        bool ok = _screenOpts.ResolveAbsoluteCoordsFromRatios(refRect);
        if (ok && usingWindow)
            _logger.LogInformation("重定位成功：游戏窗口({Proc}) 客户区={Rect}", _screenOpts.GameProcessName, refRect);
        else if (usingWindow)
            _logger.LogWarning("重定位: ResolveAbsoluteCoordsFromRatios 返回 false，窗口={Rect}，保留旧坐标", refRect);
        else
            _logger.LogWarning("重定位: 未找到游戏窗口({Proc})，使用显示器 #{Idx} Fallback",
                _screenOpts.GameProcessName, _screenOpts.OutputIndex);
    }

    /// <summary>将 OverlayTextAlignment 映射为 WPF TextAlignment。</summary>
    private static System.Windows.TextAlignment ToWpfAlignment(OverlayTextAlignment a) => a switch
    {
        OverlayTextAlignment.Left => System.Windows.TextAlignment.Left,
        OverlayTextAlignment.Center => System.Windows.TextAlignment.Center,
        OverlayTextAlignment.Right => System.Windows.TextAlignment.Right,
        _ => System.Windows.TextAlignment.Left,
    };

    /// <summary>根据显示配置计算屏幕绝对坐标（覆盖屏物理像素 + 屏幕偏移）。</summary>
    private System.Windows.Point ResolveDisplayPosition(OverlayItemDisplayConfig config)
    {
        var (x, y) = config.ResolveToAbsolute(_overlayPhysicalBounds.Width, _overlayPhysicalBounds.Height);
        return new System.Windows.Point(x + _overlayPhysicalBounds.X, y + _overlayPhysicalBounds.Y);
    }

    private void OnFrameAnalyzed(AnalyzedFrame analysis)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (_overlayOpts.ShowDetectionBoxes)
                RefreshDetectionBoxes(analysis);
            else
                RemoveDetectionBoxes();
        });
    }

    private DrawingInfo? _fpsItem;

    private void OnFrameTiming(FrameTimingInfo t)
    {
        if (!_overlayOpts.ShowTimingStats) return;
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (_fpsItem != null) OverlayItems.Remove(_fpsItem);
            string text = $"截取:{t.CaptureMs:F0}ms 预处理:{t.YoloPreprocessMs:F0}ms 推理:{t.YoloInferenceMs:F0}ms 后处理:{t.YoloPostprocessMs:F0}ms 融合:{t.FusionMs:F0}ms 路由:{t.RoutingMs:F0}ms 分析:{t.AnalysisMs:F0}ms 追踪:{t.TrackingMs:F0}ms | 总:{t.TotalMs:F0}ms ({t.Fps:F0}fps)";
            var cmd = new TextCommand
            {
                Text = text,
                Position = ResolveDisplayPosition(_overlayOpts.TimingStatsDisplay),
                FontSize = 15,
                Alignment = ToWpfAlignment(_overlayOpts.TimingStatsDisplay.Alignment),
                VerticalAnchor = VerticalAnchor.Top,
                Foreground = new SolidColorBrush(Color.FromArgb(230, 180, 255, 180)),
                Background = new SolidColorBrush(Color.FromArgb(180, 20, 20, 20))
            };
            _fpsItem = new MahjongTileDrawingInfo(Array.Empty<DetectionResult>(), new List<IDrawingCommand> { cmd });
            OverlayItems.Add(_fpsItem);
        });
    }

    private DrawingInfo? _analysisItem;
    private DrawingInfo? _remainingItem;

    private void OnTileAnalysisReady(TileAnalysisResult r)
    {
        // 牌堆剩余牌
        if (_overlayOpts.ShowRemainingTiles)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (_remainingItem != null) OverlayItems.Remove(_remainingItem);
                var remaining = r.RemainingCounts.Where(kv => kv.Value > 0)
                    .OrderBy(kv => (int)kv.Key)
                    .Select(kv => $"{kv.Key}:{kv.Value}");
                string text = "剩余: " + string.Join(" ", remaining);
                var cmd = new TextCommand
                {
                    Text = text,
                    Position = ResolveDisplayPosition(_overlayOpts.RemainingTilesDisplay),
                    FontSize = 12,
                    Alignment = ToWpfAlignment(_overlayOpts.RemainingTilesDisplay.Alignment),
                    Foreground = new SolidColorBrush(Color.FromArgb(200, 180, 220, 255)),
                    Background = new SolidColorBrush(Color.FromArgb(160, 20, 20, 20))
                };
                _remainingItem = new MahjongTileDrawingInfo(Array.Empty<DetectionResult>(), new List<IDrawingCommand> { cmd });
                OverlayItems.Add(_remainingItem);
            });
        }
        else if (_remainingItem != null)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                OverlayItems.Remove(_remainingItem);
                _remainingItem = null;
            });
        }

        if (!_overlayOpts.ShowWinningAnalysis) return;
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (_analysisItem != null) OverlayItems.Remove(_analysisItem);

            var sb = new System.Text.StringBuilder();
            string header = r.Shanten switch
            {
                -1 => "和了!",
                0 => $"听牌! 等牌 {r.WinOptions.Count} 种",
                _ => $"向听数: {r.Shanten}"
            };
            sb.AppendLine(header);

            if (r.IsTenpai)
            {
                foreach (var w in r.WinOptions.OrderByDescending(w => w.Points))
                {
                    string tileName = w.WinTile.ToString();
                    string yakuStr = w.YakuNames.Count > 0
                        ? string.Join(" ", w.YakuNames.Take(4))
                        : "";
                    sb.AppendLine($"  {tileName} 剩{w.Remaining}张 {w.Han}翻{w.Fu}符 {w.Points}点  {yakuStr}");
                }
            }
            else if (r.DiscardOptions.Count > 0)
            {
                sb.AppendLine("--- 打牌推荐 ---");
                foreach (var d in r.DiscardOptions.Take(5))
                {
                    string tileName = d.DiscardTile.ToString();
                    sb.AppendLine($"  打{tileName} → 向听{d.ShantenAfter} 受入{d.UniqueUkeire}种{d.TotalUkeire}枚");
                }
            }

            var cmd = new TextCommand
            {
                Text = sb.ToString().TrimEnd(),
                Position = ResolveDisplayPosition(_overlayOpts.WinningAnalysisDisplay),
                FontSize = 13,
                Alignment = ToWpfAlignment(_overlayOpts.WinningAnalysisDisplay.Alignment),
                VerticalAnchor = VerticalAnchor.Top,
                Foreground = new SolidColorBrush(Color.FromArgb(230, 255, 220, 140)),
                Background = new SolidColorBrush(Color.FromArgb(180, 20, 20, 20))
            };
            _analysisItem = new MahjongTileDrawingInfo(Array.Empty<DetectionResult>(), new List<IDrawingCommand> { cmd });
            OverlayItems.Add(_analysisItem);
        });
    }

    /// <summary>用当前帧的识别结果替换所有检测框。</summary>
    private void RefreshDetectionBoxes(AnalyzedFrame analysis)
    {
        RemoveDetectionBoxes();

        // 手牌
        foreach (SeatPosition seat in Enum.GetValues<SeatPosition>())
        {
            if (analysis.Players.TryGetValue(seat, out var player) && player.HandTiles.Count > 0)
            {
                var dets = _needsCoordMapping
                    ? player.HandTiles.Select(MapDetection).ToList()
                    : player.HandTiles;
                var commands = dets.SelectMany(d => _commandGenerator.GenerateCommands(d)).ToList();
                OverlayItems.Add(new PlayerTileDrawingInfo(seat, dets, commands));
            }
        }

        // 副露
        foreach (SeatPosition seat in Enum.GetValues<SeatPosition>())
        {
            if (analysis.Players.TryGetValue(seat, out var player) && player.Melds.Count > 0)
            {
                foreach (var meld in player.Melds)
                {
                    var dets = _needsCoordMapping
                        ? meld.Tiles.Select(MapDetection).ToList()
                        : meld.Tiles;
                    var commands = dets.SelectMany(d => _commandGenerator.GenerateCommands(d, meldType: meld.MeldType)).ToList();
                    OverlayItems.Add(new PlayerTileDrawingInfo(seat, dets, commands));
                }
            }
        }

        // 弃牌
        foreach (SeatPosition seat in Enum.GetValues<SeatPosition>())
        {
            if (analysis.DiscardPondDetections.TryGetValue(seat, out var pondDets) && pondDets.Count > 0)
            {
                var dets = _needsCoordMapping
                    ? pondDets.Select(MapDetection).ToList()
                    : pondDets;
                var commands = dets.SelectMany(d => _commandGenerator.GenerateCommands(d)).ToList();
                OverlayItems.Add(new PlayerTileDrawingInfo(seat, dets, commands));
            }
        }

        // 宝牌指示牌
        if (analysis.DoraIndicatorDetections.Count > 0)
        {
            var dets = _needsCoordMapping
                ? analysis.DoraIndicatorDetections.Select(MapDetection).ToList()
                : analysis.DoraIndicatorDetections;
            var commands = dets.SelectMany(d => _commandGenerator.GenerateCommands(d)).ToList();
            OverlayItems.Add(new MahjongTileDrawingInfo(dets, commands));
        }

    }

    /// <summary>移除所有检测框项（保留区域项）。</summary>
    private void RemoveDetectionBoxes()
    {
        var toRemove = OverlayItems
            .Where(i => i is not ScreenRegionDrawingInfo && i != _fpsItem && i != _analysisItem && i != _remainingItem)
            .ToList();
        foreach (var item in toRemove)
            OverlayItems.Remove(item);
    }

    // ─────────────── 区域绘制（静态数据，非帧级） ───────────────

    private void DrawScreenRegions()
    {
        if (_regionItems.Count > 0) return; // 已绘制

        AddRegionQuad("Self Hand+Meld", _screenOpts.SelfHandAndMeldArea, Colors.LimeGreen);
        AddRegionQuad("Right Hand+Meld", _screenOpts.RightHandAndMeldArea, Colors.DodgerBlue);
        AddRegionQuad("Opposite Hand+Meld", _screenOpts.OppositeHandAndMeldArea, Colors.OrangeRed);
        AddRegionQuad("Left Hand+Meld", _screenOpts.LeftHandAndMeldArea, Colors.Gold);

        AddRegionQuad("Self Discard", _screenOpts.SelfDiscardPondArea, Color.FromRgb(100, 200, 100));
        AddRegionQuad("Right Discard", _screenOpts.RightDiscardPondArea, Color.FromRgb(100, 150, 220));
        AddRegionQuad("Opposite Discard", _screenOpts.OppositeDiscardPondArea, Color.FromRgb(220, 130, 110));
        AddRegionQuad("Left Discard", _screenOpts.LeftDiscardPondArea, Color.FromRgb(200, 180, 80));

        AddRegionQuad("Dora Indicator", _screenOpts.DoraIndicatorArea, Colors.Magenta);
        AddRegionQuad("Info", _screenOpts.InfoArea, Colors.Cyan);
    }

    private void AddRegionQuad(string name, CvPoint[] quad, Color color)
    {
        if (quad.Length != 4) return;
        if (quad.All(p => p.X == 0 && p.Y == 0)) return;

        // 跨屏映射
        if (_needsCoordMapping)
            quad = MapPoints(quad);

        var points = new PointCollection();
        for (int i = 0; i < 4; i++)
            points.Add(new System.Windows.Point(quad[i].X, quad[i].Y));

        var commands = new List<IDrawingCommand>
        {
            new PolygonCommand
            {
                Points = points,
                IsClosed = true,
                IsFilled = true
            },
            new TextCommand
            {
                Text = name,
                Position = new System.Windows.Point(
                    (points[0].X + points[1].X + points[2].X + points[3].X) / 4,
                    (points[0].Y + points[1].Y + points[2].Y + points[3].Y) / 4),
                FontSize = 16,
                Alignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(color),
                Background = new SolidColorBrush(Colors.Transparent)
            }
        };

        var item = new ScreenRegionDrawingInfo(name, commands, color);
        _regionItems.Add(item);
        OverlayItems.Add(item);
    }

    private void RemoveScreenRegions()
    {
        foreach (var item in _regionItems)
            OverlayItems.Remove(item);
        _regionItems.Clear();
    }

    // ─────────────── 跨屏坐标映射 ───────────────

    /// <summary>将截取屏物理像素坐标映射为覆盖屏物理像素坐标。</summary>
    private CvRect MapRect(CvRect r)
    {
        float sx = _captureBounds.Width > 0 ? (r.X - _captureBounds.X) / _captureBounds.Width : 0;
        float sy = _captureBounds.Height > 0 ? (r.Y - _captureBounds.Y) / _captureBounds.Height : 0;
        float sw = _captureBounds.Width > 0 ? r.Width / _captureBounds.Width : 0;
        float sh = _captureBounds.Height > 0 ? r.Height / _captureBounds.Height : 0;
        return new CvRect(
            (int)Math.Round(sx * _overlayPhysicalBounds.Width + _overlayPhysicalBounds.X),
            (int)Math.Round(sy * _overlayPhysicalBounds.Height + _overlayPhysicalBounds.Y),
            (int)Math.Round(sw * _overlayPhysicalBounds.Width),
            (int)Math.Round(sh * _overlayPhysicalBounds.Height));
    }

    private CvPoint[] MapPoints(CvPoint[] pts)
    {
        var mapped = new CvPoint[pts.Length];
        for (int i = 0; i < pts.Length; i++)
        {
            float sx = _captureBounds.Width > 0 ? (pts[i].X - _captureBounds.X) / _captureBounds.Width : 0;
            float sy = _captureBounds.Height > 0 ? (pts[i].Y - _captureBounds.Y) / _captureBounds.Height : 0;
            mapped[i] = new CvPoint(
                (int)Math.Round(sx * _overlayPhysicalBounds.Width + _overlayPhysicalBounds.X),
                (int)Math.Round(sy * _overlayPhysicalBounds.Height + _overlayPhysicalBounds.Y));
        }
        return mapped;
    }

    private DetectionResult MapDetection(DetectionResult d)
    {
        return d with { BoundingBox = MapRect(d.BoundingBox) };
    }
}
