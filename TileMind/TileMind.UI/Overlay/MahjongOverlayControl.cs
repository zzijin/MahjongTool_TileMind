using System.Windows.Media;
using TileMind.UI.Overlay.OverlayBase;
using TileMind.UI.Overlay.OverlayBase.DrawingCommand;

namespace TileMind.UI.Overlay;

public class MahjongOverlayControl : OverlayBaseControl
{
    protected override (Brush fillBrush, Pen strokePen) GetDrawingStyles(DrawingInfo info)
    {
        // 区域标记：半透明填充 + 实线边框
        if (info is ScreenRegionDrawingInfo regionInfo)
        {
            var color = regionInfo.FillColor;
            var fill = new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B));
            var pen = new Pen(new SolidColorBrush(color), 1.5);
            return (fill, pen);
        }

        if (info is PlayerTileDrawingInfo playerInfo)
            return GetPlayerTileStyles(playerInfo);

        if (info is MahjongTileDrawingInfo)
            return GetDefaultTileStyles();

        return (Brushes.Transparent, new Pen(Brushes.Transparent, 0));
    }

    private (Brush fillBrush, Pen strokePen) GetPlayerTileStyles(PlayerTileDrawingInfo info)
    {
        var color = info.Seat switch
        {
            Common.Models.SeatPosition.Self     => Colors.LimeGreen,
            Common.Models.SeatPosition.Right    => Colors.DodgerBlue,
            Common.Models.SeatPosition.Opposite => Colors.OrangeRed,
            Common.Models.SeatPosition.Left     => Colors.Gold,
            _                                  => Colors.LimeGreen
        };

        byte alpha = (byte)(FillOpacity * 255);
        var fill = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
        var pen = new Pen(new SolidColorBrush(color), StrokeThickness);
        return (fill, pen);
    }

    private static (Brush fillBrush, Pen strokePen) GetDefaultTileStyles()
    {
        var fill = new SolidColorBrush(Color.FromArgb(76, 50, 205, 50));
        var pen = new Pen(new SolidColorBrush(Colors.LimeGreen), 2);
        return (fill, pen);
    }
}
