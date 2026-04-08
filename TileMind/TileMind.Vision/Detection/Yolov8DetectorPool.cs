using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System.Collections.Concurrent;


namespace TileMind.Vision.Detection
{
    /// <summary>
    /// 基于对象池的 YOLOv8 检测器管理器，用于高性能多线程推理。
    /// </summary>
    public class Yolov8DetectorPool : IDisposable
    {
        private readonly ConcurrentBag<Yolov8Detector> _pool = new ConcurrentBag<Yolov8Detector>();
        private readonly string _modelPath;
        private readonly string[] _classNames;
        private readonly float _confidenceThreshold;
        private readonly float _iouThreshold;
        private readonly bool _useCuda;
        private bool _disposed;

        public Yolov8DetectorPool(string modelPath, string[] classNames, float confidenceThreshold = 0.5f, float iouThreshold = 0.5f, bool useCuda = false)
        {
            _modelPath = modelPath;
            _classNames = classNames;
            _confidenceThreshold = confidenceThreshold;
            _iouThreshold = iouThreshold;
            _useCuda = useCuda;
        }

        /// <summary>
        /// 从池中获取一个 Yolov8Detector 实例。如果池为空，则创建一个新实例。
        /// </summary>
        public Yolov8Detector Rent()
        {
            if (_pool.TryTake(out var detector))
            {
                return detector;
            }
            // 如果池中没有可用对象，则创建一个新的
            return new Yolov8Detector(_modelPath, _classNames, _confidenceThreshold, _iouThreshold, _useCuda);
        }

        /// <summary>
        /// 将 Yolov8Detector 实例归还到池中。
        /// </summary>
        public void Return(Yolov8Detector detector)
        {
            if (detector == null) return;
            _pool.Add(detector);
        }

        /// <summary>
        /// 预先在池中创建指定数量的实例，以预热对象池。
        /// </summary>
        public void PreWarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                _pool.Add(new Yolov8Detector(_modelPath, _classNames, _confidenceThreshold, _iouThreshold, _useCuda));
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                while (_pool.TryTake(out var detector))
                {
                    detector.Dispose();
                }
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}