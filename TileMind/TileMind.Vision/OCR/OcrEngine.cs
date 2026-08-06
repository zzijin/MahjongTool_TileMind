using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using TileMind.Common.Config;

namespace TileMind.Vision.OCR;

/// <summary>
/// 基于 ONNX Runtime 的 OCR 引擎。GPU 优先，CPU 回退。
/// </summary>
public class OcrEngine : IDisposable
{
    private readonly ILogger<OcrEngine> _logger;
    private readonly InferenceSession _detSession;
    private readonly InferenceSession _recSession;
    private readonly IReadOnlyList<string> _charDict;
    private readonly string _deviceName;
    private bool _disposed;

    /// <summary>引擎是否已加载模型。</summary>
    public bool IsReady { get; }

    /// <summary>推理设备名称。</summary>
    public string DeviceName => _deviceName;

    public OcrEngine(OcrOptions opts, ILogger<OcrEngine> logger)
    {
        _logger = logger;

        var sessionOptions = new SessionOptions();
        if (opts.GpuDeviceId >= 0)
        {
            try
            {
                sessionOptions.AppendExecutionProvider_CUDA(opts.GpuDeviceId);
                _deviceName = $"CUDA:{opts.GpuDeviceId}";
                _logger.LogInformation("OCR 引擎使用 CUDA GPU {GpuId}", opts.GpuDeviceId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CUDA 加载失败，回退 CPU");
                sessionOptions.AppendExecutionProvider_CPU();
                _deviceName = "CPU";
            }
        }
        else
        {
            sessionOptions.AppendExecutionProvider_CPU();
            _deviceName = "CPU";
        }

        // 检测模型
        try
        {
            _detSession = new InferenceSession(opts.DetModelPath, sessionOptions);
            _logger.LogInformation("检测模型已加载: {Path}", opts.DetModelPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检测模型加载失败: {Path}", opts.DetModelPath);
            throw;
        }

        // 识别模型
        try
        {
            _recSession = new InferenceSession(opts.RecModelPath, sessionOptions);
            _logger.LogInformation("识别模型已加载: {Path}", opts.RecModelPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "识别模型加载失败: {Path}", opts.RecModelPath);
            _detSession.Dispose();
            throw;
        }

        // 字符字典
        try
        {
            var json = File.ReadAllText(opts.RecCharDictPath);
            _charDict = JsonSerializer.Deserialize<List<string>>(json)
                        ?? throw new InvalidOperationException("字符字典 JSON 格式无效");
            _logger.LogInformation("字符字典已加载: {Path} ({Count} 字符)", opts.RecCharDictPath, _charDict.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "字符字典加载失败: {Path}", opts.RecCharDictPath);
            _detSession.Dispose();
            _recSession.Dispose();
            throw;
        }

        IsReady = true;
    }

    // ── 检测 ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// 文字检测：输入 BGR 图像，返回文本框坐标、置信度和裁剪图像。
    /// </summary>
    public (List<Point2f[]> Boxes, List<float> Scores, List<Mat> Crops) Detect(Mat imageBgr)
    {
        if (!IsReady) throw new InvalidOperationException("OCR 引擎未就绪");

        var (tensor, shapeInfo) = OcrPreprocess.PreprocessDet(imageBgr);

        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("x", tensor) };
        using var results = _detSession.Run(inputs);
        var output = results[0].AsTensor<float>() as DenseTensor<float>
                     ?? throw new InvalidOperationException("检测模型输出不是 DenseTensor<float>");

        var (boxes, scores) = OcrPostprocess.ExtractBoxes(
            output, (imageBgr.Rows, imageBgr.Cols), shapeInfo);
        var crops = OcrPostprocess.CropRegions(imageBgr, boxes);

        _logger.LogDebug("检测完成: {Count} 个文本框", boxes.Count);
        return (boxes, scores, crops);
    }

    // ── 识别 ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// 文字识别：对裁剪图像列表做批量识别。
    /// </summary>
    public List<(string Text, float Confidence)> Recognize(List<Mat> crops)
    {
        if (crops.Count == 0) return [];
        if (!IsReady) throw new InvalidOperationException("OCR 引擎未就绪");

        var tensor = OcrPreprocess.PreprocessRecBatch(crops);

        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("x", tensor) };
        using var results = _recSession.Run(inputs);
        var logits = results[0].AsTensor<float>() as DenseTensor<float>
                     ?? throw new InvalidOperationException("识别模型输出不是 DenseTensor<float>");

        var decoded = OcrPostprocess.CtcDecodeBatch(logits, _charDict);
        _logger.LogDebug("识别完成: {Count} 个", decoded.Count);
        return decoded;
    }

    // ── 一站式 ────────────────────────────────────────────────────────────────

    /// <summary>一站式识别：检测 → 裁剪 → 识别。</summary>
    public List<(string Text, float Confidence, Point2f[] Box)> Predict(Mat imageBgr)
    {
        var (boxes, scores, crops) = Detect(imageBgr);
        if (boxes.Count == 0) return [];

        var recResults = Recognize(crops);
        var results = new List<(string, float, Point2f[])>(Math.Min(boxes.Count, recResults.Count));
        for (int i = 0; i < Math.Min(boxes.Count, recResults.Count); i++)
            results.Add((recResults[i].Text, recResults[i].Confidence, boxes[i]));

        foreach (var crop in crops) crop.Dispose();
        _logger.LogInformation("Predict 完成: {Count} 条", results.Count);
        return results;
    }

    // ── 释放 ──────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _detSession.Dispose();
        _recSession.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
