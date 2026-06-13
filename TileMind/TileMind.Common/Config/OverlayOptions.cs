namespace TileMind.Common.Config;

/// <summary>
/// 覆盖层显示开关配置（可持久化到 JSON）。
/// </summary>
public class OverlayOptions
{
    public const string SettingFilePath = @".\settings\overlaysettings.json";

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

    /// <summary>玩家动作记录显示。</summary>
    public bool ShowActionLog { get; set; }

    /// <summary>AI 决策显示。</summary>
    public bool ShowAIDecision { get; set; }
}
