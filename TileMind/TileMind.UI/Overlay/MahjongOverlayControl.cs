using System.Windows.Media;
using TileMind.UI.Overlay.OverlayBase;
using TileMind.UI.Overlay.OverlayBase.DrawingCommand;

namespace TileMind.UI.Overlay;

public class MahjongOverlayControl : OverlayBaseControl
{
    protected override (Brush fillBrush, Pen strokePen) GetDrawingStyles(DrawingInfo info)
    {
        if (info is PlayerTileDrawingInfo playerInfo)
            return GetPlayerTileStyles(playerInfo);

        // 兼容旧的 MahjongTileDrawingInfo（无玩家信息，默认绿色）
        if (info is MahjongTileDrawingInfo tileInfo)
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
        var fill = new SolidColorBrush(Color.FromArgb(76, 50, 205, 50)); // 0.3 * 255 = 76
        var pen = new Pen(new SolidColorBrush(Colors.LimeGreen), 2);
        return (fill, pen);
    }
}
