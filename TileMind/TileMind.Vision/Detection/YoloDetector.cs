using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TileMind.Vision.Detection
{
    /// <summary>
    /// 高性能 YOLOv8 目标检测器，基于 Microsoft.ML.OnnxRuntime。
    /// </summary>
    public class Yolov8Detector : IDisposable
    {
        private readonly InferenceSession _session;
        private readonly float _confidenceThreshold;
        private readonly float _iouThreshold;
        private readonly string _inputName;
        private readonly int _inputWidth;
        private readonly int _inputHeight;
        private readonly string[] _classNames;

        private bool _disposed = false;

        // YOLOv8 模型输入尺寸,目前默认是1280
        private const int DefaultInputSize = 1280;

        /// <summary>
        /// 初始化 YOLOv8 检测器。
        /// </summary>
        /// <param name="modelPath">ONNX 模型文件路径。</param>
        /// <param name="classNames">类别名称数组，顺序需与模型训练时一致。</param>
        /// <param name="confidenceThreshold">置信度阈值，低于此值的检测框将被忽略。</param>
        /// <param name="iouThreshold">交并比(IoU)阈值，用于非极大值抑制(NMS)。</param>
        /// <param name="useCuda">是否尝试使用 CUDA GPU 加速。</param>
        public Yolov8Detector(string modelPath, string[] classNames, float confidenceThreshold = 0.5f, float iouThreshold = 0.45f, bool useCuda = false)
        {
            _classNames = classNames ?? throw new ArgumentNullException(nameof(classNames));
            _confidenceThreshold = confidenceThreshold;
            _iouThreshold = iouThreshold;

            // 1. 配置推理会话选项
            var sessionOptions = new SessionOptions();
            if (useCuda)
            {
                try
                {
                    sessionOptions.AppendExecutionProvider_CUDA(); // 尝试启用 CUDA
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"警告: 无法启用 CUDA 加速，将回退到 CPU。原因: {ex.Message}");
                    sessionOptions.AppendExecutionProvider_CPU(); // 回退到 CPU
                }
            }
            else
            {
                sessionOptions.AppendExecutionProvider_CPU();
            }

            // 2. 加载 ONNX 模型，创建推理会话
            _session = new InferenceSession(modelPath, sessionOptions);

            // 3. 获取模型元数据
            // YOLOv8 模型通常只有一个名为 "images" 的输入
            _inputName = _session.InputMetadata.Keys.First();
            var inputShape = _session.InputMetadata[_inputName].Dimensions;
            _inputHeight = inputShape[2]; // 通常为 640
            _inputWidth = inputShape[3];  // 通常为 640

            if (_inputHeight != DefaultInputSize || _inputWidth != DefaultInputSize)
            {
                Console.WriteLine($"警告: 模型输入尺寸为 {_inputHeight}x{_inputWidth}，与默认的 640x640 不同，预处理会自动适配。");
            }
        }

        /// <summary>
        /// 对单张图片进行目标检测。
        /// </summary>
        /// <param name="imagePath">图片文件路径。</param>
        /// <returns>检测结果列表。</returns>
        public List<DetectionResult> Detect(string imagePath)
        {
            using var image = new Mat(imagePath);
            return Detect(image);
        }

        /// <summary>
        /// 对 OpenCvSharp.Mat 对象进行目标检测。
        /// </summary>
        /// <param name="image">输入的图像 Mat 对象。</param>
        /// <returns>检测结果列表。</returns>
        public List<DetectionResult> Detect(Mat image)
        {
            if (image.Empty())
                throw new ArgumentException("输入图像不能为空。");

            // 记录原始图像尺寸，用于将预测坐标缩放回原图
            var originalWidth = image.Width;
            var originalHeight = image.Height;

            // --- 图像预处理 ---
            var inputTensor = PreprocessImage(image);

            // --- 执行推理 ---
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_inputName, inputTensor) };
            using var results = _session.Run(inputs);

            // YOLOv8 模型只有一个输出，包含了所有检测信息
            var outputName = _session.OutputMetadata.Keys.First();
            var outputTensor = results.First(x => x.Name == outputName).AsTensor<float>();

            // --- 后处理 (解析输出 + NMS) ---
            var detections = PostprocessOutput(outputTensor, originalWidth, originalHeight);

            return detections;
        }

        /// <summary>
        /// 图像预处理：调整大小、归一化、转换为张量。
        /// </summary>
        private Tensor<float> PreprocessImage(Mat image)
        {
            // 创建一个 letterbox 区域，保持宽高比，并用灰色填充
            var letterboxedImage = new Mat(_inputHeight, _inputWidth, MatType.CV_8UC3, Scalar.Gray);
            var scale = Math.Min((float)_inputWidth / image.Width, (float)_inputHeight / image.Height);
            var scaledWidth = (int)(image.Width * scale);
            var scaledHeight = (int)(image.Height * scale);
            var offsetX = (_inputWidth - scaledWidth) / 2;
            var offsetY = (_inputHeight - scaledHeight) / 2;

            using var resizedImage = new Mat();
            Cv2.Resize(image, resizedImage, new Size(scaledWidth, scaledHeight));
            resizedImage.CopyTo(new Mat(letterboxedImage, new Rect(offsetX, offsetY, scaledWidth, scaledHeight)));

            // BGR (OpenCV 默认) 转 RGB，并转换为 float32
            using var rgbImage = new Mat();
            Cv2.CvtColor(letterboxedImage, rgbImage, ColorConversionCodes.BGR2RGB);
            rgbImage.ConvertTo(rgbImage, MatType.CV_32FC3, 1.0 / 255.0); // 归一化到 [0, 1]

            // 创建输入张量 (1, 3, Height, Width)
            var inputTensor = new DenseTensor<float>(new[] { 1, 3, _inputHeight, _inputWidth });

            // 将图像数据复制到张量中 (CHW 格式)
            for (int y = 0; y < _inputHeight; y++)
            {
                for (int x = 0; x < _inputWidth; x++)
                {
                    var pixel = rgbImage.At<Vec3f>(y, x);
                    inputTensor[0, 0, y, x] = pixel.Item0; // R
                    inputTensor[0, 1, y, x] = pixel.Item1; // G
                    inputTensor[0, 2, y, x] = pixel.Item2; // B
                }
            }

            return inputTensor;
        }

        /// <summary>
        /// 后处理：解析模型输出，执行 NMS，将坐标映射回原图尺寸。
        /// </summary>
        private List<DetectionResult> PostprocessOutput(Tensor<float> outputTensor, int originalWidth, int originalHeight)
        {
            var detections = new List<DetectionResult>();

            // YOLOv8 输出形状为 (1, 84, 8400) 或 (1, 8400, 84)
            // 84 = 4 (bbox坐标) + 80 (COCO数据集类别数) 或 + N (自定义类别数)
            int numClasses = _classNames.Length;
            int dimensions = outputTensor.Dimensions[1] == 84 ? outputTensor.Dimensions[1] : outputTensor.Dimensions[2];
            int numAnchors = outputTensor.Dimensions[1] == 84 ? outputTensor.Dimensions[2] : outputTensor.Dimensions[1];

            if (dimensions != 4 + numClasses)
            {
                throw new InvalidOperationException($"模型输出维度 ({dimensions}) 与提供的类别数量 ({numClasses}) 不匹配 (应为 4 + {numClasses})。");
            }

            // 计算缩放和偏移量，用于将坐标映射回原图
            var scale = Math.Min((float)DefaultInputSize / originalWidth, (float)DefaultInputSize / originalHeight);
            var scaledWidth = (int)(originalWidth * scale);
            var scaledHeight = (int)(originalHeight * scale);
            var offsetX = (DefaultInputSize - scaledWidth) / 2f;
            var offsetY = (DefaultInputSize - scaledHeight) / 2f;
            var scaleX = (float)originalWidth / scaledWidth;
            var scaleY = (float)originalHeight / scaledHeight;

            for (int i = 0; i < numAnchors; i++)
            {
                // 获取当前 anchor 的所有数据
                var data = new float[dimensions];
                for (int j = 0; j < dimensions; j++)
                {
                    data[j] = outputTensor.Dimensions[1] == 84 ? outputTensor[0, j, i] : outputTensor[0, i, j];
                }

                // 提取 bbox 坐标 (cx, cy, w, h) 并转换为中心点格式
                float cx = data[0];
                float cy = data[1];
                float w = data[2];
                float h = data[3];

                // 获取最大类别置信度
                float maxClassConfidence = 0;
                int maxClassIndex = -1;
                for (int k = 4; k < dimensions; k++)
                {
                    if (data[k] > maxClassConfidence)
                    {
                        maxClassConfidence = data[k];
                        maxClassIndex = k - 4;
                    }
                }

                // 置信度筛选
                if (maxClassConfidence < _confidenceThreshold)
                    continue;

                // 将 cx, cy, w, h 转换为 x1, y1, x2, y2 (在 640x640 平面内)
                float x1 = cx - w / 2;
                float y1 = cy - h / 2;
                float x2 = cx + w / 2;
                float y2 = cy + h / 2;

                // 调整坐标，去除 letterbox 填充并映射回原始图像尺寸
                x1 = (x1 - offsetX) * scaleX;
                y1 = (y1 - offsetY) * scaleY;
                x2 = (x2 - offsetX) * scaleX;
                y2 = (y2 - offsetY) * scaleY;

                // 边界裁剪
                x1 = Math.Max(0, Math.Min(originalWidth, x1));
                y1 = Math.Max(0, Math.Min(originalHeight, y1));
                x2 = Math.Max(0, Math.Min(originalWidth, x2));
                y2 = Math.Max(0, Math.Min(originalHeight, y2));

                detections.Add(new DetectionResult
                {
                    ClassId = maxClassIndex,
                    ClassName = _classNames[maxClassIndex],
                    Confidence = maxClassConfidence,
                    BoundingBox = new Rect((int)x1, (int)y1, (int)(x2 - x1), (int)(y2 - y1))
                });
            }

            // 应用非极大值抑制 (NMS)
            return ApplyNms(detections);
        }

        /// <summary>
        /// 对检测结果应用非极大值抑制 (NMS)。
        /// </summary>
        private List<DetectionResult> ApplyNms(List<DetectionResult> detections)
        {
            var nmsResults = new List<DetectionResult>();
            // 按置信度降序排序
            var sortedDetections = detections.OrderByDescending(d => d.Confidence).ToList();

            while (sortedDetections.Any())
            {
                var best = sortedDetections.First();
                nmsResults.Add(best);
                sortedDetections.RemoveAt(0);

                // 移除与当前最佳框 IoU 过高的框
                sortedDetections.RemoveAll(d =>
                {
                    var iou = CalculateIoU(best.BoundingBox, d.BoundingBox);
                    return iou > _iouThreshold;
                });
            }

            return nmsResults;
        }

        /// <summary>
        /// 计算两个矩形的交并比 (IoU)。
        /// </summary>
        private float CalculateIoU(Rect a, Rect b)
        {
            var intersection = Rect.Intersect(a, b);
            if (intersection.Width <= 0 || intersection.Height <= 0)
                return 0;

            float intersectionArea = intersection.Width * intersection.Height;
            float unionArea = (a.Width * a.Height) + (b.Width * b.Height) - intersectionArea;
            return intersectionArea / unionArea;
        }

        /// <summary>
        /// 释放资源。
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _session?.Dispose();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// 检测结果类。
    /// </summary>
    public class DetectionResult
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; }
        public float Confidence { get; set; }
        public Rect BoundingBox { get; set; }
    }
}