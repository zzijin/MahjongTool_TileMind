using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using TileMind.Common.Config;
using TileMind.Common.Helpers;
using TileMind.Common.Models;
using TileMind.UI.Views;
using TileMind.Vision.ScreenCapture;
using PointF = System.Drawing.PointF;
using RectangleF = System.Drawing.RectangleF;

namespace TileMind.UI.ViewModels;

public partial class ScreenSplitterViewModel : ViewModel
{
    private readonly ScreenCaptureOptions _options;
    private readonly MonitorService _monitorService;
    private readonly ILogger<ScreenSplitterViewModel> _logger;
    private ScreenSplitterOverlayControl? _control;

    public ScreenSplitterViewModel(ScreenCaptureOptions options, MonitorService monitorService, ILogger<ScreenSplitterViewModel> logger)
    {
        _options = options;
        _monitorService = monitorService;
        _logger = logger;
    }

    public void SetControl(ScreenSplitterOverlayControl control) => _control = control;

    /// <summary>获取标定窗口应显示在的显示器信息。</summary>
    public MonitorInfo? GetTargetMonitor()
    {
        return _monitorService.FindByOutputIndex(_options.OutputIndex);
    }

    public void LoadConfig()
    {
        if (_control == null) return;
        _control.LoadFromOptions(_options);
    }

    /// <summary>
    /// 保存：从 Quad 写入绝对坐标 → 反算 Ratio → 存 JSON → 重算绝对坐标。
    /// </summary>
    [RelayCommand]
    private void SaveConfig()
    {
        if (_control == null) return;

        _control.WriteToOptions(_options);

        // 反算 Ratio
        var refRect = GetReferenceRect();
        SaveRatiosFromAbsolute(refRect);

        // 用新 Ratio 重算绝对坐标（一致性验证）
        _options.ResolveAbsoluteCoordsFromRatios(refRect);
        _options.Save();

        _logger.LogInformation("配置已保存，Ratio 已更新 (参照矩形={RefRect})。", refRect);
    }

    /// <summary>
    /// 重定位：重新查找游戏窗口 → Ratio 解析到绝对坐标 → 刷新 Quad 显示。
    /// 失败时保留旧坐标，记录警告。
    /// </summary>
    [RelayCommand]
    private void Relocate()
    {
        if (_control == null) return;

        var refRect = GetReferenceRect();
        bool ok = _options.ResolveAbsoluteCoordsFromRatios(refRect);

        if (ok)
        {
            _logger.LogInformation("重定位成功：游戏窗口找到，参照矩形={RefRect}", refRect);
        }
        else
        {
            _logger.LogWarning("重定位: ResolveAbsoluteCoordsFromRatios 返回 false，保留旧坐标。参照矩形={RefRect}", refRect);
        }

        LoadConfig(); // 刷新 Quad 显示
    }

    /// <summary>
    /// 恢复默认 Ratio 并立即保存。
    /// </summary>
    [RelayCommand]
    private void ResetToDefault()
    {
        if (_control == null) return;

        var defaults = new ScreenCaptureOptions();
        _options.DoraIndicatorRatio = defaults.DoraIndicatorRatio;
        _options.TableRatio = defaults.TableRatio;
        _options.DiscardPondRatio = defaults.DiscardPondRatio;
        _options.InfoRatio = defaults.InfoRatio;

        var refRect = GetReferenceRect();
        _options.ResolveAbsoluteCoordsFromRatios(refRect);
        _options.Save();

        LoadConfig();
        _logger.LogInformation("Ratio 已恢复默认并保存 (参照矩形={RefRect})。", refRect);
    }

    // ─────────────── 辅助 ───────────────

    /// <summary>获取参照矩形：优先游戏窗口客户区，否则 Fallback 到显示器边界。</summary>
    private RectangleF GetReferenceRect()
    {
        var clientRect = WindowFinderHelper.FindClientRect(_options.GameProcessName ?? "");
        if (clientRect.HasValue)
            return clientRect.Value;

        var monitor = _monitorService.FindByOutputIndex(_options.OutputIndex);
        if (monitor != null)
            return monitor.Bounds;

        _logger.LogDebug("未找到游戏窗口({Process})和显示器 #{Index}，使用空矩形。",
            _options.GameProcessName, _options.OutputIndex);
        return new RectangleF();
    }

    /// <summary>从当前绝对坐标反算 Ratio 并写入 _options。</summary>
    private void SaveRatiosFromAbsolute(RectangleF refRect)
    {
        if (refRect.Width <= 0 || refRect.Height <= 0) return;

        _options.DoraIndicatorRatio = ToRatio(_options.DoraIndicatorArea, refRect);
        _options.TableRatio = ToRatio(_options.TableArea, refRect);
        _options.DiscardPondRatio = ToRatio(_options.DiscardPondArea, refRect);
        _options.InfoRatio = ToRatio(_options.InfoArea, refRect);
    }

    private static PointF[] ToRatio(Point[] abs, RectangleF refRect)
    {
        var ratio = new PointF[abs.Length];
        for (int i = 0; i < abs.Length; i++)
        {
            ratio[i] = new PointF(
                (abs[i].X - refRect.X) / refRect.Width,
                (abs[i].Y - refRect.Y) / refRect.Height);
        }
        return ratio;
    }
}
