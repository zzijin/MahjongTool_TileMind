using System.Windows.Media;
using TileMind.UI.Overlay.OverlayBase;
using TileMind.UI.Overlay.OverlayBase.DrawingCommand;

namespace TileMind.UI.Overlay;

/// <summary>
/// 屏幕识别区域的绘制信息，用于在覆盖层中标记配置的各个识别区域。
/// </summary>
public class ScreenRegionDrawingInfo : DrawingInfo<string>
{
    /// <summary>区域填充色（用于 OverlayControl 获取样式）。</summary>
    public Color FillColor { get; }

    public ScreenRegionDrawingInfo(string regionName, List<IDrawingCommand> commands, Color fillColor)
        : base(regionName, commands)
    {
        FillColor = fillColor;
    }
}
