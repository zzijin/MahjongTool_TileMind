using System;
using System.Collections.Generic;
using System.Text;

namespace TileMind.Common.Config
{
    public class FrameFusionOptions
    {
        public const string SettingFilePath = @".\settings\framefusionsettings.json";
        public const string SectionName = "FrameFusion";

        //是否启用帧融合功能，启用后识别准确率可能提升，但会增加处理延迟，建议在性能允许的情况下启用（实验性）
        public bool EnableFusion { get; set; } = false;

        //最大融合帧数,单次采集这么多帧数后才开始融合,过多可能增加延迟
        public int MaxFusionFrameCount { get; set; } = 3;

        //帧间变化阈值，超过此值认为场景发生变化
        public float MovementThreshold { get; set; } = 0.01f; 

        //融合置信度，单帧识别结果的置信度必须达到该值才会参与融合
        public float FusionConfidenceThreshold { get; set; } = 0.40f;

        //融合IoU阈值，融合时如果不同帧间的两条检测结果的IoU超过该值则认为是同一目标
        public float FusionIouThreshold { get; set; } = 0.80f;
    }
}
