using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using TileMind.Vision.Interop;

namespace TileMind.Vision.OCR;

/// <summary>
/// OCR 检测和识别模型的图像预处理。
/// </summary>
public static class OcrPreprocess
{
    // ── 检测预处理参数 ──

    private const int DetTargetLong = 960;
    private const int DetStride = 128;
    private static readonly float[] DetMean = [0.485f, 0.456f, 0.406f];
    private static readonly float[] DetStd = [0.229f, 0.224f, 0.225f];

    // ── 识别预处理参数 ──

    public const int RecImgH = 48;
    public const int RecImgW = 320;
    public const int RecMaxW = 3200;

    // ── 检测 ──────────────────────────────────────────

    /// <summary>Letterbox resize：长边缩放到 DetTargetLong，Stride 对齐。</summary>
    public static (Mat Resized, (int srcH, int srcW, int newH, int newW) ShapeInfo)
        LetterboxResize(Mat imageBgr)
    {
        int h = imageBgr.Rows, w = imageBgr.Cols;
        double ratio = (double)DetTargetLong / Math.Max(h, w);
        int newH = (int)(Math.Round(h * ratio / DetStride) * DetStride);
        int newW = (int)(Math.Round(w * ratio / DetStride) * DetStride);
        if (newH < DetStride) newH = DetStride;
        if (newW < DetStride) newW = DetStride;

        var resized = new Mat();
        Cv2.Resize(imageBgr, resized, new Size(newW, newH));
        return (resized, (h, w, newH, newW));
    }

    /// <summary>检测模型标准化：BGR → float32 [0,1] → Split → (x - mean) / std → Span CopyTo tensor。</summary>
    public static DenseTensor<float> DetNormalize(Mat imageBgr)
    {
        int h = imageBgr.Rows, w = imageBgr.Cols;
        int planeSize = h * w;

        using var f32 = new Mat();
        imageBgr.ConvertTo(f32, MatType.CV_32FC3, 1.0 / 255.0);
        var channels = Cv2.Split(f32);

        var tensor = new DenseTensor<float>(new[] { 1, 3, h, w });
        var dst = tensor.Buffer.Span;

        for (int c = 0; c < 3; c++)
        {
            using var meanMat = new Mat(h, w, MatType.CV_32FC1, new Scalar(DetMean[c]));
            using var stdMat = new Mat(h, w, MatType.CV_32FC1, new Scalar(DetStd[c]));
            Cv2.Subtract(channels[c], meanMat, channels[c]);
            Cv2.Divide(channels[c], stdMat, channels[c]);

            MatSpanInterop.AsFloatSpan(channels[c], planeSize).CopyTo(dst.Slice(c * planeSize, planeSize));
        }
        foreach (var ch in channels) ch.Dispose();

        return tensor;
    }

    /// <summary>检测预处理全流程：Letterbox → 标准化。</summary>
    public static (DenseTensor<float> Tensor, (int srcH, int srcW, int newH, int newW) ShapeInfo)
        PreprocessDet(Mat imageBgr)
    {
        var (resized, shapeInfo) = LetterboxResize(imageBgr);
        var tensor = DetNormalize(resized);
        resized.Dispose();
        return (tensor, shapeInfo);
    }

    // ── 识别（单张） ────────────────────────────────────

    /// <summary>单张文字区域预处理：Resize → Normalize → tensor。</summary>
    public static DenseTensor<float> PreprocessRec(Mat cropBgr)
    {
        int h = cropBgr.Rows, w = cropBgr.Cols;
        float whRatio = (float)w / h;
        int resizedW = (int)Math.Ceiling(RecImgH * Math.Max((float)RecImgW / RecImgH, whRatio));
        resizedW = Math.Min(resizedW, RecMaxW);

        using var resized = new Mat();
        Cv2.Resize(cropBgr, resized, new Size(resizedW, RecImgH));
        using var f32 = new Mat();
        resized.ConvertTo(f32, MatType.CV_32FC3, 1.0 / 255.0);

        var channels = Cv2.Split(f32);
        for (int c = 0; c < 3; c++)
        {
            using var half = new Mat(RecImgH, resizedW, MatType.CV_32FC1, new Scalar(0.5));
            Cv2.Subtract(channels[c], half, channels[c]);
            Cv2.Multiply(channels[c], 2.0, channels[c]);
        }

        var tensor = new DenseTensor<float>(new[] { 1, 3, RecImgH, RecImgW });
        var dst = tensor.Buffer.Span;
        int planeSize = RecImgH * RecImgW;
        int srcRowSize = resizedW;

        for (int c = 0; c < 3; c++)
        {
            var src = MatSpanInterop.AsFloatSpan(channels[c], srcRowSize * RecImgH);
            var chDst = dst.Slice(c * planeSize, planeSize);
            for (int y = 0; y < RecImgH; y++)
                src.Slice(y * srcRowSize, srcRowSize).CopyTo(chDst.Slice(y * RecImgW, srcRowSize));
        }
        foreach (var ch in channels) ch.Dispose();

        return tensor;
    }

    // ── 识别（批量） ────────────────────────────────────

    /// <summary>多张文字区域批量预处理：Resize → Normalize → batch tensor，按最宽对齐。</summary>
    public static DenseTensor<float> PreprocessRecBatch(List<Mat> crops)
    {
        int batch = crops.Count;
        var widths = new int[batch];
        int maxW = RecImgW;
        for (int i = 0; i < batch; i++)
        {
            int rw = (int)Math.Ceiling(RecImgH *
                Math.Max((float)RecImgW / RecImgH, (float)crops[i].Cols / crops[i].Rows));
            rw = Math.Min(rw, RecMaxW);
            widths[i] = rw;
            if (rw > maxW) maxW = rw;
        }

        int imgW = maxW;
        var tensor = new DenseTensor<float>(new[] { batch, 3, RecImgH, imgW });
        var dst = tensor.Buffer.Span;
        int planeSize = RecImgH * imgW;
        int singleSize = 3 * planeSize;

        for (int i = 0; i < batch; i++)
        {
            int resizedW = widths[i];
            var crop = crops[i];

            using var resized = new Mat();
            Cv2.Resize(crop, resized, new Size(resizedW, RecImgH));
            using var f32 = new Mat();
            resized.ConvertTo(f32, MatType.CV_32FC3, 1.0 / 255.0);

            var channels = Cv2.Split(f32);
            for (int c = 0; c < 3; c++)
            {
                using var half = new Mat(RecImgH, resizedW, MatType.CV_32FC1, new Scalar(0.5));
                Cv2.Subtract(channels[c], half, channels[c]);
                Cv2.Multiply(channels[c], 2.0, channels[c]);
            }

            int batchOff = i * singleSize;
            int srcSize = resizedW * RecImgH;
            for (int c = 0; c < 3; c++)
            {
                var src = MatSpanInterop.AsFloatSpan(channels[c], srcSize);
                var chDst = dst.Slice(batchOff + c * planeSize, planeSize);
                for (int y = 0; y < RecImgH; y++)
                    src.Slice(y * resizedW, resizedW).CopyTo(chDst.Slice(y * imgW, resizedW));
            }
            foreach (var ch in channels) ch.Dispose();
        }

        return tensor;
    }
}
