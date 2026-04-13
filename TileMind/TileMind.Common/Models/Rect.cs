using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text;

namespace TileMind.Common.Models
{
    public record struct Rect
    {
        //
        // 摘要:
        //     Gets the y-coordinate of the top edge of this Rect structure.
        public int Top
        {
            readonly get
            {
                return Y;
            }
            set
            {
                Y = value;
            }
        }

        //
        // 摘要:
        //     Gets the y-coordinate that is the sum of the Y and Height property values of
        //     this Rect structure.
        public int Bottom => Y + Height;

        //
        // 摘要:
        //     Gets the x-coordinate of the left edge of this Rect structure.
        public int Left
        {
            get
            {
                return X;
            }
            set
            {
                X = value;
            }
        }

        //
        // 摘要:
        //     Gets the x-coordinate that is the sum of X and Width property values of this
        //     Rect structure.
        public int Right => X + Width;

        //
        // 摘要:
        //     Coordinate of the left-most rectangle corner [Point(X, Y)]
        public Point Location
        {
            get
            {
                return new Point(X, Y);
            }
            set
            {
                X = value.X;
                Y = value.Y;
            }
        }

        //
        // 摘要:
        //     Size of the rectangle [CvSize(Width, Height)]
        public Size Size
        {
            get
            {
                return new Size(Width, Height);
            }
            set
            {
                Width = value.Width;
                Height = value.Height;
            }
        }

        //
        // 摘要:
        //     Coordinate of the left-most rectangle corner [Point(X, Y)]
        public Point TopLeft => new Point(X, Y);

        //
        // 摘要:
        //     Coordinate of the right-most rectangle corner [Point(X+Width, Y+Height)]
        public Point BottomRight => new Point(X + Width, Y + Height);

        //
        // 摘要:
        //     The x-coordinate of the upper-left corner of the rectangle.
        public int X;

        //
        // 摘要:
        //     The y-coordinate of the upper-left corner of the rectangle.
        public int Y;

        //
        // 摘要:
        //     The width of the rectangle.
        public int Width;

        //
        // 摘要:
        //     The height of the rectangle.
        public int Height;

        //
        // 摘要:
        //     Stores a set of four integers that represent the location and size of a rectangle
        //
        //
        // 参数:
        //   X:
        //     The x-coordinate of the upper-left corner of the rectangle.
        //
        //   Y:
        //     The y-coordinate of the upper-left corner of the rectangle.
        //
        //   Width:
        //     The width of the rectangle.
        //
        //   Height:
        //     The height of the rectangle.
        public Rect(int X, int Y, int Width, int Height)
        {
            this.X = X;
            this.Y = Y;
            this.Width = Width;
            this.Height = Height;
        }

        //
        // 摘要:
        //     Initializes a new instance of the Rectangle class with the specified location
        //     and size.
        //
        // 参数:
        //   location:
        //     A Point that represents the upper-left corner of the rectangular region.
        //
        //   size:
        //     A Size that represents the width and height of the rectangular region.
        public Rect(Point location, Size size)
            : this(location.X, location.Y, size.Width, size.Height)
        {
        }

        //
        // 摘要:
        //     Creates a Rectangle structure with the specified edge locations.
        //
        // 参数:
        //   left:
        //     The x-coordinate of the upper-left corner of this Rectangle structure.
        //
        //   top:
        //     The y-coordinate of the upper-left corner of this Rectangle structure.
        //
        //   right:
        //     The x-coordinate of the lower-right corner of this Rectangle structure.
        //
        //   bottom:
        //     The y-coordinate of the lower-right corner of this Rectangle structure.
        public static Rect FromLTRB(int left, int top, int right, int bottom)
        {
            Rect result = new Rect
            {
                X = left,
                Y = top,
                Width = right - left,
                Height = bottom - top
            };
            if (result.Width < 0)
            {
                throw new ArgumentException("right > left");
            }

            if (result.Height < 0)
            {
                throw new ArgumentException("bottom > top");
            }

            return result;
        }

        //
        // 摘要:
        //     Shifts rectangle by a certain offset
        //
        // 参数:
        //   rect:
        //
        //   pt:
        public static Rect operator +(Rect rect, Point pt)
        {
            return rect.Add(pt);
        }

        //
        // 摘要:
        //     Shifts rectangle by a certain offset
        //
        // 参数:
        //   pt:
        public readonly Rect Add(Point pt)
        {
            return this with
            {
                X = X + pt.X,
                Y = Y + pt.Y
            };
        }

        //
        // 摘要:
        //     Shifts rectangle by a certain offset
        //
        // 参数:
        //   rect:
        //
        //   pt:
        public static Rect operator -(Rect rect, Point pt)
        {
            return rect with
            {
                X = rect.X - pt.X,
                Y = rect.Y - pt.Y
            };
        }

        //
        // 摘要:
        //     Shifts rectangle by a certain offset
        //
        // 参数:
        //   pt:
        public readonly Rect Subtract(Point pt)
        {
            return this with
            {
                X = X - pt.X,
                Y = Y - pt.Y
            };
        }

        //
        // 摘要:
        //     Expands or shrinks rectangle by a certain amount
        //
        // 参数:
        //   rect:
        //
        //   size:
        public static Rect operator +(Rect rect, Size size)
        {
            return rect with
            {
                Width = rect.Width + size.Width,
                Height = rect.Height + size.Height
            };
        }

        //
        // 摘要:
        //     Expands or shrinks rectangle by a certain amount
        //
        // 参数:
        //   size:
        public readonly Rect Add(Size size)
        {
            return this with
            {
                Width = Width + size.Width,
                Height = Height + size.Height
            };
        }

        //
        // 摘要:
        //     Expands or shrinks rectangle by a certain amount
        //
        // 参数:
        //   rect:
        //
        //   size:
        public static Rect operator -(Rect rect, Size size)
        {
            return rect with
            {
                Width = rect.Width - size.Width,
                Height = rect.Height - size.Height
            };
        }

        //
        // 摘要:
        //     Expands or shrinks rectangle by a certain amount
        //
        // 参数:
        //   size:
        public readonly Rect Subtract(Size size)
        {
            return this with
            {
                Width = Width - size.Width,
                Height = Height - size.Height
            };
        }

        //
        // 摘要:
        //     Determines the Rect structure that represents the intersection of two rectangles.
        //
        //
        // 参数:
        //   a:
        //     A rectangle to intersect.
        //
        //   b:
        //     A rectangle to intersect.
        public static Rect operator &(Rect a, Rect b)
        {
            return Intersect(a, b);
        }

        //
        // 摘要:
        //     Gets a Rect structure that contains the union of two Rect structures.
        //
        // 参数:
        //   a:
        //     A rectangle to union.
        //
        //   b:
        //     A rectangle to union.
        public static Rect operator |(Rect a, Rect b)
        {
            return Union(a, b);
        }

        //
        // 摘要:
        //     Determines if the specified point is contained within the rectangular region
        //     defined by this Rectangle.
        //
        // 参数:
        //   x:
        //     x-coordinate of the point
        //
        //   y:
        //     y-coordinate of the point
        public readonly bool Contains(int x, int y)
        {
            if (X <= x && Y <= y && X + Width > x)
            {
                return Y + Height > y;
            }

            return false;
        }

        //
        // 摘要:
        //     Determines if the specified point is contained within the rectangular region
        //     defined by this Rectangle.
        //
        // 参数:
        //   pt:
        //     point
        public readonly bool Contains(Point pt)
        {
            return Contains(pt.X, pt.Y);
        }

        //
        // 摘要:
        //     Determines if the specified rectangle is contained within the rectangular region
        //     defined by this Rectangle.
        //
        // 参数:
        //   rect:
        //     rectangle
        public readonly bool Contains(Rect rect)
        {
            if (X <= rect.X && rect.X + rect.Width <= X + Width && Y <= rect.Y)
            {
                return rect.Y + rect.Height <= Y + Height;
            }

            return false;
        }

        //
        // 摘要:
        //     Inflates this Rect by the specified amount.
        //
        // 参数:
        //   width:
        //     The amount to inflate this Rectangle horizontally.
        //
        //   height:
        //     The amount to inflate this Rectangle vertically.
        public void Inflate(int width, int height)
        {
            X -= width;
            Y -= height;
            Width += 2 * width;
            Height += 2 * height;
        }

        //
        // 摘要:
        //     Inflates this Rect by the specified amount.
        //
        // 参数:
        //   size:
        //     The amount to inflate this rectangle.
        public void Inflate(Size size)
        {
            Inflate(size.Width, size.Height);
        }

        //
        // 摘要:
        //     Creates and returns an inflated copy of the specified Rect structure.
        //
        // 参数:
        //   rect:
        //     The Rectangle with which to start. This rectangle is not modified.
        //
        //   x:
        //     The amount to inflate this Rectangle horizontally.
        //
        //   y:
        //     The amount to inflate this Rectangle vertically.
        public static Rect Inflate(Rect rect, int x, int y)
        {
            rect.Inflate(x, y);
            return rect;
        }

        //
        // 摘要:
        //     Determines the Rect structure that represents the intersection of two rectangles.
        //
        //
        // 参数:
        //   a:
        //     A rectangle to intersect.
        //
        //   b:
        //     A rectangle to intersect.
        public static Rect Intersect(Rect a, Rect b)
        {
            int num = Math.Max(a.X, b.X);
            int num2 = Math.Min(a.X + a.Width, b.X + b.Width);
            int num3 = Math.Max(a.Y, b.Y);
            int num4 = Math.Min(a.Y + a.Height, b.Y + b.Height);
            if (num2 >= num && num4 >= num3)
            {
                return new Rect(num, num3, num2 - num, num4 - num3);
            }

            return default(Rect);
        }

        //
        // 摘要:
        //     Determines the Rect structure that represents the intersection of two rectangles.
        //
        //
        // 参数:
        //   rect:
        //     A rectangle to intersect.
        public readonly Rect Intersect(Rect rect)
        {
            return Intersect(this, rect);
        }

        //
        // 摘要:
        //     Determines if this rectangle intersects with rect.
        //
        // 参数:
        //   rect:
        //     Rectangle
        public readonly bool IntersectsWith(Rect rect)
        {
            if (X < rect.X + rect.Width && X + Width > rect.X && Y < rect.Y + rect.Height)
            {
                return Y + Height > rect.Y;
            }

            return false;
        }

        //
        // 摘要:
        //     Gets a Rect structure that contains the union of two Rect structures.
        //
        // 参数:
        //   rect:
        //     A rectangle to union.
        public readonly Rect Union(Rect rect)
        {
            return Union(this, rect);
        }

        //
        // 摘要:
        //     Gets a Rect structure that contains the union of two Rect structures.
        //
        // 参数:
        //   a:
        //     A rectangle to union.
        //
        //   b:
        //     A rectangle to union.
        public static Rect Union(Rect a, Rect b)
        {
            int num = Math.Min(a.X, b.X);
            int num2 = Math.Max(a.X + a.Width, b.X + b.Width);
            int num3 = Math.Min(a.Y, b.Y);
            int num4 = Math.Max(a.Y + a.Height, b.Y + b.Height);
            return new Rect(num, num3, num2 - num, num4 - num3);
        }
    }
}
