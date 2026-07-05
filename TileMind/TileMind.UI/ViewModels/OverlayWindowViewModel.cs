using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Windows;
using System.Windows.Media;
using TileMind.Common.Config;
using TileMind.Common.Helpers;
using TileMind.Common.Models;
using TileMind.Core.Services;
using TileMind.UI.Overlay;
using TileMind.UI.Overlay.OverlayBase;
using TileMind.UI.Overlay.OverlayBase.DrawingCommand;
using TileMind.Vision.ScreenCapture;
using CvPoint = OpenCvSharp.Point;
using CvRect = OpenCvSharp.Rect;
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

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartPipelineCommand))]
    private bool _isPipelineRunning;

    public OverlayOptions OverlayOptions => _overlayOpts;

    // ── 截取/覆盖屏信息 ──
    private RectangleF _captureBounds;
    private RectangleF _overlayPhysicalBounds;
    private bool _needsCoordMapping;

    // ── 覆盖层控件引用 ──
    private OverlayBaseControl? _overlayControl;

    // ── 各层最新数据 ──
    private AnalyzedFrame? _latestAnalysis;
    private FrameTimingInfo? _latestTiming;
    private TileAnalysisResult? _latestAnalysisResult;
    private bool _renderPending;

    /// <summary>缓存的区域命令（只生成一次）。</summary>
    private IReadOnlyList<IDrawingCommand> _regionCommands = Array.Empty<IDrawingCommand>();
    private bool _regionsBuilt;

    /// <summary>缓存的样式画刷。</summary>
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

        var captureMonitor = monitorService.FindByOutputIndex(screenOpts.OutputIndex);
        var overlayMonitor = monitorService.FindByOutputIndex(overlayOpts.OutputIndex);
        if (captureMonitor != null) _captureBounds = captureMonitor.Bounds;
        if (overlayMonitor != null) _overlayPhysicalBounds = overlayMonitor.Bounds;

        _needsCoordMapping = screenOpts.OutputIndex != overlayOpts.OutputIndex
                             && captureMonitor != null && overlayMonitor != null;

        hub.FrameAnalyzed += OnFrameAnalyzed;
        hub.FrameTiming += OnFrameTiming;
        hub.TileAnalysisReady += OnTileAnalysisReady;
    }

    /// <summary>由 OverlayWindow 设置控件引用。</summary>
    public void SetOverlayControl(OverlayBaseControl control) => _overlayControl = control;

    // ── 事件处理：更新状态 + 排队渲染 ──

    private void OnFrameAnalyzed(AnalyzedFrame analysis)
    {
        _latestAnalysis = analysis;
        QueueRender();
    }

    private void OnFrameTiming(FrameTimingInfo t)
    {
        _latestTiming = t;
        QueueRender();
    }

    private void OnTileAnalysisReady(TileAnalysisResult r)
    {
        _latestAnalysisResult = r;
        QueueRender();
    }

    private void QueueRender()
    {
        if (_renderPending) return;
        _renderPending = true;
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _renderPending = false;
            BuildAndSubmit();
        });
    }

    // ── 每帧全量构建命令 ──

    private void BuildAndSubmit()
    {
        if (_overlayControl == null) return;

        // 区域（只生成一次）
        if (!_regionsBuilt && _overlayOpts.ShowScreenRegions)
        {
            _regionCommands = BuildRegionCommands();
            _regionsBuilt = true;
        }

        var commands = new List<IDrawingCommand>();
        commands.AddRange(_regionCommands);

        // 检测框
        if (_overlayOpts.ShowDetectionBoxes && _latestAnalysis != null)
            commands.AddRange(BuildDetectionCommands(_latestAnalysis));

        // 耗时统计
        if (_overlayOpts.ShowTimingStats && _latestTiming != null)
            commands.Add(BuildTimingCommand(_latestTiming));

        // 牌堆剩余牌
        if (_overlayOpts.ShowRemainingTiles && _latestAnalysisResult != null)
            commands.Add(BuildRemainingCommand(_latestAnalysisResult));

        // 牌型分析
        if (_overlayOpts.ShowWinningAnalysis && _latestAnalysisResult != null)
            commands.AddRange(BuildAnalysisCommands(_latestAnalysisResult));

        _overlayControl.SubmitFrame(commands);
    }

    // ── 区域命令（缓存） ──

    private IReadOnlyList<IDrawingCommand> BuildRegionCommands()
    {
        var cmds = new List<IDrawingCommand>();

        void AddQuad(string name, CvPoint[] quad, Color color)
        {
            if (quad.Length != 4) return;
            if (quad.All(p => p.X == 0 && p.Y == 0)) return;
            if (_needsCoordMapping) quad = MapPoints(quad);

            var pts = new PointCollection();
            for (int i = 0; i < 4; i++) pts.Add(new System.Windows.Point(quad[i].X, quad[i].Y));
            var fill = new SolidColorBrush(Color.FromArgb(60, color.R, color.G, color.B));
            var pen = new Pen(new SolidColorBrush(color), 1.5);

            cmds.Add(new PolygonCommand { Points = pts, IsClosed = true, IsFilled = true, FillBrush = fill, StrokePen = pen });
            cmds.Add(new TextCommand
            {
                Text = name,
                Position = new System.Windows.Point((quad[0].X + quad[2].X) / 2, (quad[0].Y + quad[2].Y) / 2),
                FontSize = 14, Alignment = TextAlignment.Center, Foreground = new SolidColorBrush(color),
                Background = Brushes.Transparent,
            });
        }

        AddQuad("Self Hand+Meld", _screenOpts.SelfHandAndMeldArea, Colors.LimeGreen);
        AddQuad("Right Hand+Meld", _screenOpts.RightHandAndMeldArea, Colors.DodgerBlue);
        AddQuad("Opposite Hand+Meld", _screenOpts.OppositeHandAndMeldArea, Colors.OrangeRed);
        AddQuad("Left Hand+Meld", _screenOpts.LeftHandAndMeldArea, Colors.Gold);
        AddQuad("Self Discard", _screenOpts.SelfDiscardPondArea, Color.FromRgb(100, 200, 100));
        AddQuad("Right Discard", _screenOpts.RightDiscardPondArea, Color.FromRgb(100, 150, 220));
        AddQuad("Opposite Discard", _screenOpts.OppositeDiscardPondArea, Color.FromRgb(220, 130, 110));
        AddQuad("Left Discard", _screenOpts.LeftDiscardPondArea, Color.FromRgb(200, 180, 80));
        AddQuad("Dora Indicator", _screenOpts.DoraIndicatorArea, Colors.Magenta);
        AddQuad("Info", _screenOpts.InfoArea, Colors.Cyan);

        return cmds;
    }

    // ── 检测框命令 ──

    private List<IDrawingCommand> BuildDetectionCommands(AnalyzedFrame analysis)
    {
        var cmds = new List<IDrawingCommand>();
        var emptyPen = new Pen(Brushes.Transparent, 0);

        // 手牌 + 副露
        foreach (SeatPosition seat in Enum.GetValues<SeatPosition>())
        {
    
            if (analysis.Players.TryGetValue(seat, out var player))
            {
                foreach (var d in player.HandTiles)
                {
                    var mapped = _needsCoordMapping ? MapDetection(d) : d;
                    cmds.AddRange(_commandGenerator.GenerateCommands(mapped));
                }
                foreach (var meld in player.Melds)
                {
                    foreach (var d in meld.Tiles)
                    {
                        var mapped = _needsCoordMapping ? MapDetection(d) : d;
                        cmds.AddRange(_commandGenerator.GenerateCommands(mapped, meldType: meld.MeldType));
                    }
                }
            }
        }

        // 弃牌
        foreach (SeatPosition seat in Enum.GetValues<SeatPosition>())
        {
                if (analysis.DiscardPondDetections.TryGetValue(seat, out var pondDets))
            {
                foreach (var d in pondDets)
                {
                    var mapped = _needsCoordMapping ? MapDetection(d) : d;
                    cmds.AddRange(_commandGenerator.GenerateCommands(mapped));
                }
            }
        }

        // 宝牌指示牌
        foreach (var d in analysis.DoraIndicatorDetections)
        {
            var mapped = _needsCoordMapping ? MapDetection(d) : d;
            cmds.AddRange(_commandGenerator.GenerateCommands(mapped));
        }

        return cmds;
    }

    // ── 文本命令 ──

    private IDrawingCommand BuildTimingCommand(FrameTimingInfo t)
    {
        string text = $"截取:{t.CaptureMs:F0}ms 预处理:{t.YoloPreprocessMs:F0}ms 推理:{t.YoloInferenceMs:F0}ms 后处理:{t.YoloPostprocessMs:F0}ms 融合:{t.FusionMs:F0}ms 路由:{t.RoutingMs:F0}ms 分析:{t.AnalysisMs:F0}ms 追踪:{t.TrackingMs:F0}ms | 总:{t.TotalMs:F0}ms ({t.Fps:F0}fps)";
        return new TextCommand
        {
            Text = text,
            Position = ResolvePosition(_overlayOpts.TimingStatsDisplay),
            FontSize = 15,
            Alignment = ToWpfAlignment(_overlayOpts.TimingStatsDisplay.Alignment),
            VerticalAnchor = _overlayOpts.TimingStatsDisplay.VerticalAnchor,
            Foreground = new SolidColorBrush(Color.FromArgb(230, 180, 255, 180)),
            Background = new SolidColorBrush(Color.FromArgb(180, 20, 20, 20))
        };
    }

    private IDrawingCommand BuildRemainingCommand(TileAnalysisResult r)
    {
        var remaining = r.RemainingCounts.Where(kv => kv.Value > 0)
            .OrderBy(kv => (int)kv.Key)
            .Select(kv => $"{kv.Key}:{kv.Value}");
        return new TextCommand
        {
            Text = "剩余: " + string.Join(" ", remaining),
            Position = ResolvePosition(_overlayOpts.RemainingTilesDisplay),
            FontSize = 12,
            Alignment = ToWpfAlignment(_overlayOpts.RemainingTilesDisplay.Alignment),
            VerticalAnchor = _overlayOpts.RemainingTilesDisplay.VerticalAnchor,
            Foreground = new SolidColorBrush(Color.FromArgb(200, 180, 220, 255)),
            Background = new SolidColorBrush(Color.FromArgb(160, 20, 20, 20))
        };
    }

    private List<IDrawingCommand> BuildAnalysisCommands(TileAnalysisResult r)
    {
        var cmds = new List<IDrawingCommand>();

        if (_overlayOpts.WinningAnalysisMode == WinningAnalysisMode.OnTile && !r.IsTenpai && r.DiscardOptions.Count > 0)
        {
            var handTiles = _latestAnalysis?.Players
                .FirstOrDefault(p => p.Key == SeatPosition.Self).Value?.HandTiles;
            if (handTiles != null)
            {
                foreach (var d in r.DiscardOptions.Take(7))
                {
                    var match = handTiles.FirstOrDefault(t => t.TileType == d.DiscardTile);
                    if (match == null) continue;
                    var mapped = _needsCoordMapping ? MapDetection(match) : match;
                    var rect = mapped.BoundingBox;

                    string label = d.ShantenAfter == 0
                        ? $"打{d.DiscardTile}→听\n{d.UniqueUkeire}种{d.TotalUkeire}枚"
                        : $"打{d.DiscardTile}\n向听{d.ShantenAfter} {d.UniqueUkeire}种{d.TotalUkeire}枚";

                    cmds.Add(new TextCommand
                    {
                        Text = label,
                        Position = new System.Windows.Point(rect.X + rect.Width / 2, rect.Y - 4),
                        FontSize = 11,
                        Alignment = TextAlignment.Center,
                        VerticalAnchor = VerticalAnchor.Bottom,
                        Foreground = new SolidColorBrush(Color.FromArgb(230, 255, 220, 140)),
                        Background = new SolidColorBrush(Color.FromArgb(180, 20, 20, 20))
                    });
                }
            }
        }
        else
        {
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
                    string yakuStr = w.YakuNames.Count > 0 ? string.Join(" ", w.YakuNames.Take(4)) : "";
                    sb.AppendLine($"  {tileName} 剩{w.Remaining}张 {w.Han}翻{w.Fu}符 {w.Points}点  {yakuStr}");
                }
            }
            else if (r.DiscardOptions.Count > 0)
            {
                sb.AppendLine("--- 打牌推荐 ---");
                foreach (var d in r.DiscardOptions.Take(5))
                    sb.AppendLine($"  打{d.DiscardTile} → 向听{d.ShantenAfter} 受入{d.UniqueUkeire}种{d.TotalUkeire}枚");
            }

            cmds.Add(new TextCommand
            {
                Text = sb.ToString().TrimEnd(),
                Position = ResolvePosition(_overlayOpts.WinningAnalysisDisplay),
                FontSize = 13,
                Alignment = ToWpfAlignment(_overlayOpts.WinningAnalysisDisplay.Alignment),
                VerticalAnchor = _overlayOpts.WinningAnalysisDisplay.VerticalAnchor,
                Foreground = new SolidColorBrush(Color.FromArgb(230, 255, 220, 140)),
                Background = new SolidColorBrush(Color.FromArgb(180, 20, 20, 20))
            });
        }

        return cmds;
    }

    // ── 位置解析 ──

    private System.Windows.Point ResolvePosition(OverlayItemDisplayConfig config)
    {
        var (x, y) = config.ResolveToAbsolute(_overlayPhysicalBounds.Width, _overlayPhysicalBounds.Height);
        return new System.Windows.Point(x + _overlayPhysicalBounds.X, y + _overlayPhysicalBounds.Y);
    }

    private static System.Windows.TextAlignment ToWpfAlignment(OverlayTextAlignment a) => a switch
    {
        OverlayTextAlignment.Left => TextAlignment.Left,
        OverlayTextAlignment.Center => TextAlignment.Center,
        OverlayTextAlignment.Right => TextAlignment.Right,
        _ => TextAlignment.Left,
    };

    // ── 坐标映射 ──

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
            mapped[i] = new CvPoint(
                (int)Math.Round((pts[i].X - _captureBounds.X) / _captureBounds.Width * _overlayPhysicalBounds.Width + _overlayPhysicalBounds.X),
                (int)Math.Round((pts[i].Y - _captureBounds.Y) / _captureBounds.Height * _overlayPhysicalBounds.Height + _overlayPhysicalBounds.Y));
        }
        return mapped;
    }

    private DetectionResult MapDetection(DetectionResult d) => d with { BoundingBox = MapRect(d.BoundingBox) };

    // ── 流水线控制 ──

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
            catch (Exception ex) { _logger.LogError(ex, "流水线异常。"); }
            finally { _logger.LogInformation("流水线停止。"); }
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

    [RelayCommand]
    private void RelocateCoordinates()
    {
        var clientRect = WindowFinderHelper.FindClientRect(_screenOpts.GameProcessName ?? "");
        var refRect = clientRect ?? _monitorService.FindByOutputIndex(_screenOpts.OutputIndex)?.Bounds ?? new RectangleF();
        bool ok = _screenOpts.ResolveAbsoluteCoordsFromRatios(refRect);
        _logger.LogInformation(ok
            ? "重定位成功：参照矩形={Rect}"
            : "重定位: ResolveAbsoluteCoordsFromRatios 返回 false", refRect);
    }
}
