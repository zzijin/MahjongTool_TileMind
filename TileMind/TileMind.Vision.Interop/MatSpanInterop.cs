using OpenCvSharp;

namespace TileMind.Vision.Interop;

/// <summary>
/// 将 OpenCV Mat 的原始内存暴露为 Span&lt;T&gt;（零拷贝）。
/// 本项目是唯一允许 unsafe 的程序集，对外只暴露 Span/IntPtr 等结构。
/// </summary>
public static class MatSpanInterop
{
    /// <summary>
    /// 从 CV_8UC3 Mat 获取 Span&lt;Vec3b&gt;。调用方负责保证 Mat 在使用期间不被释放。
    /// </summary>
    public static unsafe Span<Vec3b> AsVec3bSpan(Mat mat)
    {
        int pixelCount = mat.Width * mat.Height;
        return new Span<Vec3b>((void*)mat.DataPointer, pixelCount);
    }

    /// <summary>
    /// 从 CV_32FC3 Mat 获取 Span&lt;Vec3f&gt;。调用方负责保证 Mat 在使用期间不被释放。
    /// </summary>
    public static unsafe Span<Vec3f> AsVec3fSpan(Mat mat)
    {
        int pixelCount = mat.Width * mat.Height;
        return new Span<Vec3f>((void*)mat.DataPointer, pixelCount);
    }

    /// <summary>
    /// 从 Mat 获取原始 byte Span。调用方负责保证 Mat 在使用期间不被释放。
    /// </summary>
    /// <param name="byteCount">期望的字节数（Width * Height * Channels * sizeof(element)）</param>
    public static unsafe Span<byte> AsByteSpan(Mat mat, int byteCount)
        => new((void*)mat.DataPointer, byteCount);
}
