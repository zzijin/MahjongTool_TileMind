using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TileMind.Common.Helpers;

namespace TileMind.Common.Config
{
    public class ScreenCaptureOptions
    {
        public const string SettingFilePath = @".\settings\screencapturesettings.json";

        //DXGI 适配器索引，通常0表示主显卡
        public int AdapterIndex { get; set; } = 0;
        //显示器索引，通常0表示主显示器
        public int OutputIndex { get; set; } = 0;

        //宝牌指示区
        public Point[] DoraIndicatorArea { get; set; } = new Point[4];

        //牌桌区域，包含所有玩家的手牌、副露区、弃牌区等
        public Point[] TableArea { get; set; } = new Point[4];

        //弃牌区域，包含所有玩家的弃牌区
        public Point[] DiscardPondArea { get; set; } = new Point[4];

        //牌桌信息区域，包含局风、剩牌数等信息显示区域
        public Point[] InfoArea { get; set; } = new Point[4];

        //以下区域通过计算获取
        //本家手牌+副露区
        [JsonIgnore]
        public Point[] SelfHandAndMeldArea { get; set; } = new Point[4];
        //本家弃牌区 
        [JsonIgnore]
        public Point[] SelfDiscardPondArea { get; set; } = new Point[4];

        //下家手牌+副露区
        [JsonIgnore]
        public Point[] RightHandAndMeldArea { get; set; } = new Point[4];
        //下家弃牌区
        [JsonIgnore]
        public Point[] RightDiscardPondArea { get; set; } = new Point[4];

        //对家手牌+副露区
        [JsonIgnore]
        public Point[] OppositeHandAndMeldArea { get; set; } = new Point[4];
        //对家弃牌区
        [JsonIgnore]
        public Point[] OppositeDiscardPondArea { get; set; } = new Point[4];

        //上家手牌+副露区
        [JsonIgnore]
        public Point[] LeftHandAndMeldArea { get; set; } = new Point[4];
        //上家弃牌区
        [JsonIgnore]
        public Point[] LeftDiscardPondArea { get; set; } = new Point[4];

        /// <summary>
        /// 使用四个基础区域（TableArea、DiscardPondArea、InfoArea）计算所有八个派生区域。
        /// 在 JSON 反序列化后、基础区域变更后、或 CopyFrom 后调用。
        /// </summary>
        public void ComputeDerivedAreas()
        {
            if (TableArea.Length != 4 || DiscardPondArea.Length != 4 || InfoArea.Length != 4)
                return;

            var B = DiscardPondArea;
            var D = InfoArea;

            // 弃牌区：QuadB 与 QuadD 之间的梯形
            SelfDiscardPondArea = new[] { B[3], D[3], D[2], B[2] };
            RightDiscardPondArea = new[] { B[2], D[2], D[1], B[1] };
            OppositeDiscardPondArea = new[] { B[1], D[1], D[0], B[0] };
            LeftDiscardPondArea = new[] { B[0], D[0], D[3], B[3] };

            // 手牌+副露区：从 QuadB 各边向外延伸与 TableArea 边界求交
            var intersectA = GeometryHelper.FindRayBoundaryIntersection(B, TableArea, 1, 0);
            var intersectB = GeometryHelper.FindRayBoundaryIntersection(B, TableArea, 2, 1);
            var intersectC = GeometryHelper.FindRayBoundaryIntersection(B, TableArea, 3, 2);
            var intersectD = GeometryHelper.FindRayBoundaryIntersection(B, TableArea, 0, 3);

            LeftHandAndMeldArea = new[] { intersectA, B[0], B[3], intersectD };
            OppositeHandAndMeldArea = new[] { intersectB, B[1], B[0], intersectA };
            RightHandAndMeldArea = new[] { intersectC, B[2], B[1], intersectB };
            SelfHandAndMeldArea = new[] { intersectD, B[3], B[2], intersectC };
        }

        /// <summary>从另一实例复制基础配置值（用于 Reload 时原地更新单例）。</summary>
        public void CopyFrom(ScreenCaptureOptions other)
        {
            AdapterIndex = other.AdapterIndex;
            OutputIndex = other.OutputIndex;
            DoraIndicatorArea = other.DoraIndicatorArea;
            TableArea = other.TableArea;
            DiscardPondArea = other.DiscardPondArea;
            InfoArea = other.InfoArea;
            ComputeDerivedAreas();
        }
    }
}
