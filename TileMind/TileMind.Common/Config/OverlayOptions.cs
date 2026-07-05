using TileMind.Common.Models;

namespace TileMind.Common.Config;

/// <summary>
/// 覆盖层显示开关与位置配置（可持久化到 JSON）。
/// 所有位置使用屏幕比值（0~1），运行时乘以实际屏幕尺寸得到绝对像素值。
/// </summary>
public class OverlayOptions
{
    public const string SettingFilePath = @".\settings\overlaysettings.json";
    public const string SectionName = "Overlay";

    /// <summary>覆盖层所在的显示器 DXGI 输出索引（0-based）。</summary>
    public int OutputIndex { get; set; } = 0;

    /// <summary>识别框显示：所有检测到的牌的矩形框和牌名标签。</summary>
    public bool ShowDetectionBoxes { get; set; } = true;

    /// <summary>识别区域显示：屏幕中标记 ScreenCaptureOptions 配置的各个区域。</summary>
    public bool ShowScreenRegions { get; set; } = true;

    /// <summary>耗时统计显示：各环节耗时与帧率。</summary>
    public bool ShowTimingStats { get; set; } = true;

    /// <summary>牌堆剩余牌数显示。</summary>
    public bool ShowRemainingTiles { get; set; }

    /// <summary>本家胡牌组合/张数/点数显示。</summary>
    public bool ShowWinningAnalysis { get; set; }

    /// <summary>牌型分析显示模式。FullText=右上角文本面板，OnTile=手牌上方标注。</summary>
    public WinningAnalysisMode WinningAnalysisMode { get; set; } = WinningAnalysisMode.FullText;

    /// <summary>玩家动作记录显示。</summary>
    public bool ShowActionLog { get; set; }

    /// <summary>AI 决策显示。</summary>
    public bool ShowAIDecision { get; set; }

    // ──────── 各显示项的位置比值与对齐 ────────

    /// <summary>耗时统计显示位置（默认左上角）。</summary>
    public OverlayItemDisplayConfig TimingStatsDisplay { get; set; } = new()
    {
        X = 0.0042f, Y = 0.0111f,
        Alignment = OverlayTextAlignment.Left,
    };

    /// <summary>牌堆剩余牌显示位置（默认右下角）。</summary>
    public OverlayItemDisplayConfig RemainingTilesDisplay { get; set; } = new()
    {
        X = 0.9958f, Y = 0.9889f,
        Alignment = OverlayTextAlignment.Right,
    };

    /// <summary>牌型分析显示位置（默认右上角）。</summary>
    public OverlayItemDisplayConfig WinningAnalysisDisplay { get; set; } = new()
    {
        X = 0.9958f, Y = 0.0222f,
        Alignment = OverlayTextAlignment.Right,
    };

    /// <summary>动作记录显示位置（默认左上角，耗时统计下方）。</summary>
    public OverlayItemDisplayConfig ActionLogDisplay { get; set; } = new()
    {
        X = 0.0042f, Y = 0.0694f,
        Alignment = OverlayTextAlignment.Left,
    };

    /// <summary>AI 决策显示位置（默认屏幕正中）。</summary>
    public OverlayItemDisplayConfig AIDecisionDisplay { get; set; } = new()
    {
        X = 0.5f, Y = 0.5f,
        Alignment = OverlayTextAlignment.Center,
    };
}
