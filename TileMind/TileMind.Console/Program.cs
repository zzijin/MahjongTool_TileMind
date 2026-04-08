

using Microsoft.Extensions.DependencyInjection;
using SharpDX;
using TileMind.Vision.Detection;
using TileMind.Vision.ScreenCapture;

namespace TileMind.Console
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Yolov8DetectorPool yolov8DetectorPool= new Yolov8DetectorPool(
                modelPath: "yolov8s.onnx",
                classNames: new[] { "class1", "class2", "class3" },
                confidenceThreshold: 0.5f,
                iouThreshold: 0.5f,
                useCuda: true
            );

            IScreenCaptureService screenCaptureService = new DxgiScreenCaptureService();
            
        }
    }
}
