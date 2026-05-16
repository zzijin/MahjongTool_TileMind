using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace TileMind.Common.Config
{
    public class YoloOptions
    {
        public const string SettingFilePath = @".\settings\yolosettings.json";
        public const string SectionName = "Yolo";

        //模型地址
        public string ModelPath { get; set; } = @".\models\yolov8m-fp32.onnx";

        //模型支持的类别名称
        public string[] ClassNames { get; set; } = [];

        //置信度
        public float ConfidenceThreshold { get; set; } = 0.40f;
        //IoU阈值
        public float IouThreshold { get; set; } = 0.50f;
        //GPU设备ID，若为-1则仅使用CPU
        public int GpuDeviceId { get; set; } = 0;
        //模型输入的图像尺寸(程序处理时使用的)
        public int InputSize { get; set; } = 1280;

        //检测器池的最小大小
        public int MinDetectorPoolSize { get; set; } = 5;
        //检测器池的最大大小
        public int MaxDetectorPoolSize { get; set; } = 10;
        //获取检测器实例的超时时间，单位秒
        public int RentTimeoutSeconds { get; set; } = 5;
    }
}
