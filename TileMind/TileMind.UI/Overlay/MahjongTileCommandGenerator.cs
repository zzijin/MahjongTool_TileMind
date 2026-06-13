using System.Windows;
using System.Windows.Media;
using TileMind.Common.Models;
using TileMind.UI.Overlay.OverlayBase.DrawingCommand;

namespace TileMind.UI.Overlay;

public class MahjongTileCommandGenerator : IDrawingCommandGenerator<DetectionResult>
{
    IEnumerable<IDrawingCommand> IDrawingCommandGenerator<DetectionResult>.GenerateCommands(DetectionResult tile)
        => GenerateCommands(tile);

    public IEnumerable<IDrawingCommand> GenerateCommands(DetectionResult tile, MeldType? meldType = null)
    {
        yield return new RectangleCommand
        {
            Rect = tile.BoundingBox.ToWRect(),
            CornerRadius = 4
        };

        string label = meldType.HasValue
            ? $"{tile.TileName} [{meldType}] {tile.Confidence:P0}"
            : $"{tile.TileName} {tile.Confidence:P0}";

        yield return new TextCommand
        {
            Text = label,
            Position = new Point(tile.BoundingBox.Left, tile.BoundingBox.Top - 5),
            FontSize = 12,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(200, 40, 40, 40))
        };
    }
}
