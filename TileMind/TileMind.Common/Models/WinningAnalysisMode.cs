namespace TileMind.Common.Models;

/// <summary>
/// 牌型分析显示模式。
/// </summary>
public enum WinningAnalysisMode
{
    /// <summary>完整文本面板（右上角）</summary>
    FullText,

    /// <summary>打牌推荐标注在手牌中对应牌的上方</summary>
    OnTile,
}
