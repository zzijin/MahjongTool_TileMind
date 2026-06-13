using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace TileMind.UI.Overlay.OverlayBase.DrawingCommand
{

    // ---------------------------------------------
    // TextCommand.cs
    public record class TextCommand : IDrawingCommand
    {
        public string Text { get; set; } = "";
        public Point Position { get; set; }                // 文本基线起点
        public double FontSize { get; set; } = 12;
        public Brush Foreground { get; set; } = Brushes.White;
        public Brush Background { get; set; } = new SolidColorBrush(Color.FromArgb(200, 40, 40, 40));
        public Typeface Typeface { get; set; } = new Typeface("Consolas");
        public TextAlignment Alignment { get; set; } = TextAlignment.Left;
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
                // 始终使用 Left，对齐通过手动位移实现。
                // 在 auto 宽度下 Center/Right 会导致 FormattedText 内部偏移与实际 Width 不一致。
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

            // Position 为锚点，textOrigin 是文字+背景的左上角
            Point textOrigin = new Point(Position.X + offsetX, Position.Y - formatted.Height);

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
