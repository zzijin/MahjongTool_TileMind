using System.Windows;
using System.Windows.Media;
using TileMind.Common.Models;

namespace TileMind.UI.Overlay.OverlayBase.DrawingCommand
{
    public record class TextCommand : IDrawingCommand
    {
        public string Text { get; set; } = "";
        /// <summary>锚点坐标。Y 的基准由 VerticalAnchor 控制。</summary>
        public Point Position { get; set; }
        public double FontSize { get; set; } = 12;
        public Brush Foreground { get; set; } = Brushes.White;
        public Brush Background { get; set; } = new SolidColorBrush(Color.FromArgb(200, 40, 40, 40));
        public Typeface Typeface { get; set; } = new Typeface("Consolas");
        /// <summary>水平对齐。</summary>
        public TextAlignment Alignment { get; set; } = TextAlignment.Left;
        /// <summary>垂直锚点，Bottom=Y 为底边，Top=Y 为顶边。默认 Bottom 兼容旧行为。</summary>
        public VerticalAnchor VerticalAnchor { get; set; } = VerticalAnchor.Bottom;
        public bool DrawBackground { get; set; } = true;

        private FormattedText CreateFormattedText(double pixelsPerDip)
        {
            return new FormattedText(
                Text,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                Typeface,
                FontSize,
                Foreground,
                pixelsPerDip)
            {
                TextAlignment = TextAlignment.Left
            };
        }

        public void Draw(DrawingContext dc, Brush fillBrush, Pen strokePen)
        {
            double pixelsPerDip = VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip;
            var formatted = CreateFormattedText(pixelsPerDip);

            double offsetX = Alignment switch
            {
                TextAlignment.Center => -formatted.Width / 2,
                TextAlignment.Right => -formatted.Width,
                _ => 0
            };

            double y = VerticalAnchor == VerticalAnchor.Top
                ? Position.Y
                : Position.Y - formatted.Height;
            Point textOrigin = new Point(Position.X + offsetX, y);

            if (DrawBackground)
            {
                double padding = 4;
                Rect bgRect = new Rect(
                    textOrigin.X - padding,
                    textOrigin.Y - padding,
                    formatted.Width + 2 * padding,
                    formatted.Height + 2 * padding);
                dc.DrawRectangle(Background, null, bgRect);
            }

            dc.DrawText(formatted, textOrigin);
        }

        public Rect GetBounds()
        {
            // 简化实现，实际可在Draw时缓存精确矩形
            return Rect.Empty;
        }
    }
}
