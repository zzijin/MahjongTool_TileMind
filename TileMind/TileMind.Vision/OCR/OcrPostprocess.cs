using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using TileMind.Vision.Interop;

namespace TileMind.Vision.OCR;

/// <summary>
/// OCR 模型输出的后处理（DB 文本框提取、CTC 解码、图像裁剪）。
/// </summary>
public static class OcrPostprocess
{
    // ── DB 后处理（检测模型输出 → 文本框） ────────────────

    /// <summary>从 DenseTensor [1,1,H,W] 提取文本框。</summary>
    public static (List<Point2f[]> Boxes, List<float> Scores) ExtractBoxes(
        DenseTensor<float> output, (int h, int w) srcShape,
        (int srcH, int srcW, int newH, int newW) shapeInfo,
        float thresh = 0.3f, float boxThresh = 0.5f, float unclipRatio = 1.5f)
    {
        int h = output.Dimensions[2];
        int w = output.Dimensions[3];
        int planeSize = h * w;
        var predMat = new Mat(h, w, MatType.CV_32FC1);
        var dst = MatSpanInterop.AsFloatSpan(predMat, planeSize);
        int idx = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                dst[idx++] = output[0, 0, y, x];

        return BoxesFromBitmap(predMat, srcShape, shapeInfo, thresh, boxThresh, unclipRatio);
    }

    /// <summary>从二值概率图提取文本框（核心算法）。</summary>
    public static (List<Point2f[]> Boxes, List<float> Scores) BoxesFromBitmap(
        Mat predMat, (int h, int w) srcShape,
        (int srcH, int srcW, int newH, int newW) shapeInfo,
        float thresh = 0.3f, float boxThresh = 0.5f, float unclipRatio = 1.5f, int minSize = 3)
    {
        int predH = predMat.Rows, predW = predMat.Cols;
        float ratioH = (float)srcShape.h / shapeInfo.newH;
        float ratioW = (float)srcShape.w / shapeInfo.newW;

        // 阈值二值化
        using var bitmap = new Mat();
        Cv2.Threshold(predMat, bitmap, thresh, 1.0, ThresholdTypes.Binary);
        bitmap.ConvertTo(bitmap, MatType.CV_8UC1, 255.0);

        Cv2.FindContours(bitmap, out var contours, out _,
            RetrievalModes.List, ContourApproximationModes.ApproxSimple);

        var boxes = new List<Point2f[]>();
        var scores = new List<float>();

        foreach (var contour in contours)
        {
            if (contour.Length < 4) continue;

            var rect = Cv2.MinAreaRect(contour);
            var points = rect.Points();

            float sideShort = Math.Min(rect.Size.Width, rect.Size.Height);
            if (sideShort < minSize) continue;

            // 框内平均得分
            using var mask = new Mat(predH, predW, MatType.CV_8UC1, Scalar.Black);
            var maskPts = points.Select(p =>
                new OpenCvSharp.Point((int)Math.Round(p.X), (int)Math.Round(p.Y))).ToArray();
            Cv2.FillPoly(mask, [maskPts], Scalar.White);
            var meanVal = Cv2.Mean(predMat, mask);
            float score = (float)meanVal.Val0;

            if (score < boxThresh) continue;

            var unclipped = UnclipPoints(points, unclipRatio);
            if (unclipped is not { Length: 4 }) continue;

            var ordered = OrderPoints(unclipped);
            for (int i = 0; i < 4; i++)
            {
                ordered[i].X = Math.Clamp(ordered[i].X * ratioW, 0, srcShape.w - 1);
                ordered[i].Y = Math.Clamp(ordered[i].Y * ratioH, 0, srcShape.h - 1);
            }

            boxes.Add(ordered);
            scores.Add(score);
        }

        // 按 Y 中心、X 排序
        var pairs = boxes.Zip(scores, (b, s) => (box: b, score: s)).ToList();
        pairs.Sort((a, b) =>
        {
            float aCY = (a.box[0].Y + a.box[2].Y) / 2f;
            float bCY = (b.box[0].Y + b.box[2].Y) / 2f;
            int yCmp = aCY.CompareTo(bCY);
            return yCmp != 0 ? yCmp : a.box[0].X.CompareTo(b.box[0].X);
        });

        return (pairs.Select(p => p.box).ToList(), pairs.Select(p => p.score).ToList());
    }

    // ── Unclip ──────────────────────────────────────────

    private static Point2f[]? UnclipPoints(Point2f[] points, float ratio)
    {
        if (points.Length != 4) return null;

        float area = (float)Cv2.ContourArea(points);
        float length = (float)Cv2.ArcLength(points, true);
        if (length <= 0) return null;

        float distance = area * ratio / length;
        if (distance <= 0) return points;

        float cx = (points[0].X + points[1].X + points[2].X + points[3].X) / 4f;
        float cy = (points[0].Y + points[1].Y + points[2].Y + points[3].Y) / 4f;

        var result = new Point2f[4];
        for (int i = 0; i < 4; i++)
        {
            float dx = points[i].X - cx, dy = points[i].Y - cy;
            float len = MathF.Sqrt(dx * dx + dy * dy);
            if (len < 1e-6f) { result[i] = points[i]; continue; }
            float scale = 1f + distance / len;
            result[i] = new Point2f(cx + dx * scale, cy + dy * scale);
        }
        return result;
    }

    // ── 角点排序 ────────────────────────────────────────

    /// <summary>将 4 点排序为 TL, TR, BR, BL。</summary>
    public static Point2f[] OrderPoints(Point2f[] pts)
    {
        if (pts.Length != 4) return pts;
        var bySum = pts.OrderBy(p => p.X + p.Y).ToArray();
        Point2f tl = bySum[0], br = bySum[3];
        var byDiff = pts.OrderBy(p => p.Y - p.X).ToArray();
        Point2f tr = byDiff[0], bl = byDiff[3];
        return [tl, tr, br, bl];
    }

    // ── CTC 解码 ────────────────────────────────────────

    /// <summary>CTC 贪婪解码：Argmax → 去重 → 去 blank → 映射字符。</summary>
    public static (string Text, float Confidence) CtcDecode(
        float[,] logits, IReadOnlyList<string> charDict)
    {
        int seqLen = logits.GetLength(0), numClasses = logits.GetLength(1);
        var indices = new int[seqLen];
        var maxProbs = new float[seqLen];

        for (int t = 0; t < seqLen; t++)
        {
            int bestIdx = 0; float bestVal = logits[t, 0];
            for (int c = 1; c < numClasses; c++)
            {
                if (logits[t, c] > bestVal) { bestVal = logits[t, c]; bestIdx = c; }
            }
            indices[t] = bestIdx; maxProbs[t] = bestVal;
        }

        var filteredIdx = new List<int>();
        var filteredProb = new List<float>();
        for (int t = 0; t < seqLen; t++)
        {
            if (indices[t] == 0) continue;
            if (t > 0 && indices[t] == indices[t - 1]) continue;
            filteredIdx.Add(indices[t]); filteredProb.Add(maxProbs[t]);
        }

        var chars = filteredIdx.Select(i =>
            i < charDict.Count ? charDict[i] : "").ToArray();
        return (string.Concat(chars),
                filteredProb.Count > 0 ? filteredProb.Average() : 0f);
    }

    /// <summary>批量 CTC 解码，形状 [batch, seqLen, numClasses]。</summary>
    public static List<(string Text, float Confidence)> CtcDecodeBatch(
        DenseTensor<float> logits, IReadOnlyList<string> charDict)
    {
        int batch = logits.Dimensions[0], seqLen = logits.Dimensions[1], numClasses = logits.Dimensions[2];
        var results = new List<(string, float)>(batch);
        for (int i = 0; i < batch; i++)
        {
            var single = new float[seqLen, numClasses];
            for (int t = 0; t < seqLen; t++)
                for (int c = 0; c < numClasses; c++)
                    single[t, c] = logits[i, t, c];
            results.Add(CtcDecode(single, charDict));
        }
        return results;
    }

    // ── 图像裁剪 ────────────────────────────────────────

    /// <summary>轴对齐裁剪文字区域。</summary>
    public static List<Mat> CropRegions(Mat imageBgr, List<Point2f[]> boxes)
    {
        var crops = new List<Mat>(boxes.Count);
        foreach (var box in boxes)
        {
            float xMin = box.Min(p => p.X), xMax = box.Max(p => p.X);
            float yMin = box.Min(p => p.Y), yMax = box.Max(p => p.Y);
            int x1 = Math.Max(0, (int)xMin), y1 = Math.Max(0, (int)yMin);
            int x2 = Math.Min(imageBgr.Cols, (int)Math.Ceiling(xMax));
            int y2 = Math.Min(imageBgr.Rows, (int)Math.Ceiling(yMax));
            crops.Add(x2 > x1 && y2 > y1
                ? imageBgr[y1..y2, x1..x2].Clone()
                : new Mat(48, 48, MatType.CV_8UC3, Scalar.Black));
        }
        return crops;
    }

    /// <summary>透视扶正后裁剪：将旋转文字区域映射为水平矩形再裁剪。</summary>
    public static List<Mat> CropRegionsStraightened(Mat imageBgr, List<Point2f[]> boxes)
    {
        var crops = new List<Mat>(boxes.Count);
        foreach (var box in boxes)
        {
            var ordered = OrderPoints(box);
            float wTop = Dist(ordered[1], ordered[0]), wBot = Dist(ordered[2], ordered[3]);
            float hLef = Dist(ordered[3], ordered[0]), hRig = Dist(ordered[2], ordered[1]);
            int dstW = (int)MathF.Max(wTop, wBot), dstH = (int)MathF.Max(hLef, hRig);

            if (dstW < 4 || dstH < 4)
            {
                crops.Add(new Mat(48, 48, MatType.CV_8UC3, Scalar.Black));
                continue;
            }

            var srcPts = new[] { ordered[0], ordered[1], ordered[2], ordered[3] };
            var dstPts = new[] {
                new Point2f(0, 0), new Point2f(dstW - 1, 0),
                new Point2f(dstW - 1, dstH - 1), new Point2f(0, dstH - 1) };
            var M = Cv2.GetPerspectiveTransform(srcPts, dstPts);
            var warped = new Mat();
            Cv2.WarpPerspective(imageBgr, warped, M, new Size(dstW, dstH),
                InterpolationFlags.Linear, BorderTypes.Replicate);
            crops.Add(warped);
        }
        return crops;
    }

    private static float Dist(Point2f a, Point2f b)
        => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
