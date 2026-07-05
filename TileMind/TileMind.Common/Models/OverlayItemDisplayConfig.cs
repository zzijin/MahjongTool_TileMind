namespace TileMind.Common.Models;

/// <summary>
/// 覆盖层单个显示项的位置和对齐配置（比值坐标，0~1 相对屏幕）。
/// </summary>
public class OverlayItemDisplayConfig
{
    /// <summary>锚点 X 坐标（屏幕比值，0~1）。</summary>
    public float X { get; set; }

    /// <summary>锚点 Y 坐标（屏幕比值，0~1）。</summary>
    public float Y { get; set; }

    /// <summary>水平对齐方式（相对于锚点）。</summary>
    public OverlayTextAlignment Alignment { get; set; } = OverlayTextAlignment.Left;

    /// <summary>垂直锚点位置。Bottom=Y为底边，Top=Y为顶边。</summary>
    public VerticalAnchor VerticalAnchor { get; set; } = VerticalAnchor.Bottom;

    /// <summary>
    /// 根据屏幕尺寸将比值位置解析为绝对像素坐标。
    /// </summary>
    public (float X, float Y) ResolveToAbsolute(double screenWidth, double screenHeight)
    {
        return (
            (float)(X * screenWidth),
            (float)(Y * screenHeight));
    }
}
