using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace TileMind.UI.Overlay.OverlayBase.DrawingCommand
{


    // ---------------------------------------------
    // RectangleCommand.cs
    public record class RectangleCommand : IDrawingCommand
    {
        public Rect Rect { get; set; } = Rect.Empty;
        public double CornerRadius { get; set; } = 0;
        public Brush? FillBrush { get; set; }
        public Pen? StrokePen { get; set; }

        public void Draw(DrawingContext dc, Brush fillBrush, Pen strokePen)
        {
            var fill = FillBrush ?? fillBrush;
            var pen = StrokePen ?? strokePen;
            if (CornerRadius > 0)
                dc.DrawRoundedRectangle(fill, pen, Rect, CornerRadius, CornerRadius);
            else
                dc.DrawRectangle(fill, pen, Rect);
        }

        public Rect GetBounds() => Rect;
    }
}
