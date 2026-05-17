using OpenCvSharp;

namespace TileMind.Common.Helpers;

public static class GeometryHelper
{
    /// <summary>
    /// 从 innerQuad 的 startIdx 顶点出发，沿 endIdx 方向向外延伸，
    /// 找到与 outerQuad（凸四边形）边界的交点。
    /// </summary>
    public static Point FindRayBoundaryIntersection(
        Point[] innerQuad, Point[] outerQuad,
        int startIdx, int endIdx)
    {
        double rx = innerQuad[startIdx].X;
        double ry = innerQuad[startIdx].Y;
        double rpx = innerQuad[endIdx].X;
        double rpy = innerQuad[endIdx].Y;

        for (int i = 0; i < 4; i++)
        {
            var s1 = outerQuad[i];
            var s2 = outerQuad[(i + 1) % 4];
            if (RayIntersectsSegment(rx, ry, rpx, rpy,
                    s1.X, s1.Y, s2.X, s2.Y,
                    out double ix, out double iy))
            {
                return new Point((int)Math.Round(ix), (int)Math.Round(iy));
            }
        }
        return default;
    }

    /// <summary>
    /// 射线与线段相交检测（纯 double 运算，无 WPF 依赖）。
    /// 射线从 (rx,ry) 出发，经过 (rpx,rpy) 并向外延伸。
    /// </summary>
    public static bool RayIntersectsSegment(
        double rx, double ry, double rpx, double rpy,
        double sx1, double sy1, double sx2, double sy2,
        out double ix, out double iy)
    {
        ix = double.NaN;
        iy = double.NaN;

        double vx = rpx - rx;
        double vy = rpy - ry;
        double wx = sx2 - sx1;
        double wy = sy2 - sy1;

        double crossVW = vx * wy - vy * wx;
        const double epsilon = 1e-10;
        if (Math.Abs(crossVW) < epsilon) return false;

        double acx = sx1 - rx;
        double acy = sy1 - ry;

        double t = (acx * wy - acy * wx) / crossVW;
        double u = (acx * vy - acy * vx) / crossVW;

        if (t >= 0 && u >= 0 && u <= 1)
        {
            ix = rx + t * vx;
            iy = ry + t * vy;
            return true;
        }
        return false;
    }
}
