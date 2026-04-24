using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Text;

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

        //本家手牌+副露区
        public Point[] SelfHandAndMeldArea { get; set; } = new Point[4];
        //本家弃牌区 
        public Point[] SelfDiscardPondArea { get; set; } = new Point[4];

        //下家手牌+副露区
        public Point[] RightHandAndMeldArea { get; set; } = new Point[4];
        //下家弃牌区
        public Point[] RightDiscardPondArea { get; set; } = new Point[4];

        //对家手牌+副露区
        public Point[] OppositeHandAndMeldArea { get; set; } = new Point[4];
        //对家弃牌区
        public Point[] OppositeDiscardPondArea { get; set; } = new Point[4];

        //上家手牌+副露区
        public Point[] LeftHandAndMeldArea { get; set; } = new Point[4];
        //上家弃牌区
        public Point[] LeftDiscardPondArea { get; set; } = new Point[4];

        public void UpdateArea(string areaType, Point topLeft, Point topRight, Point bottomRight, Point bottomLeft)
        {
            switch (areaType)
            {
                case nameof(DoraIndicatorArea):
                    {
                        DoraIndicatorArea[0] = topLeft;
                        DoraIndicatorArea[1] = topRight;
                        DoraIndicatorArea[2] = bottomRight;
                        DoraIndicatorArea[3] = bottomLeft;
                        break;
                    }
                case nameof(SelfHandAndMeldArea):
                    {
                        SelfHandAndMeldArea[0] = topLeft;
                        SelfHandAndMeldArea[1] = topRight;
                        SelfHandAndMeldArea[2] = bottomRight;
                        SelfHandAndMeldArea[3] = bottomLeft;
                        break;
                    }
                case nameof(SelfDiscardPondArea):
                    {
                        SelfDiscardPondArea[0] = topLeft;
                        SelfDiscardPondArea[1] = topRight;
                        SelfDiscardPondArea[2] = bottomRight;
                        SelfDiscardPondArea[3] = bottomLeft;
                        break;
                    }
                case nameof(RightHandAndMeldArea):
                    {
                        RightHandAndMeldArea[0] = topLeft;
                        RightHandAndMeldArea[1] = topRight;
                        RightHandAndMeldArea[2] = bottomRight;
                        RightHandAndMeldArea[3] = bottomLeft;
                        break;
                    }
                case nameof(RightDiscardPondArea):
                    {
                        RightDiscardPondArea[0] = topLeft;
                        RightDiscardPondArea[1] = topRight;
                        RightDiscardPondArea[2] = bottomRight;
                        RightDiscardPondArea[3] = bottomLeft;
                        break;
                    }
                case nameof(OppositeHandAndMeldArea):
                    {
                        OppositeHandAndMeldArea[0] = topLeft;
                        OppositeHandAndMeldArea[1] = topRight;
                        OppositeHandAndMeldArea[2] = bottomRight;
                        OppositeHandAndMeldArea[3] = bottomLeft;
                        break;
                    }
                case nameof(OppositeDiscardPondArea):
                    {
                        OppositeDiscardPondArea[0] = topLeft;
                        OppositeDiscardPondArea[1] = topRight;
                        OppositeDiscardPondArea[2] = bottomRight;
                        OppositeDiscardPondArea[3] = bottomLeft;
                        break;
                    }
                case nameof(LeftHandAndMeldArea):
                    {
                        LeftHandAndMeldArea[0] = topLeft;
                        LeftHandAndMeldArea[1] = topRight;
                        LeftHandAndMeldArea[2] = bottomRight;
                        LeftHandAndMeldArea[3] = bottomLeft;
                        break;
                    }
                case nameof(LeftDiscardPondArea):
                    {
                        LeftDiscardPondArea[0] = topLeft;
                        LeftDiscardPondArea[1] = topRight;
                        LeftDiscardPondArea[2] = bottomRight;
                        LeftDiscardPondArea[3] = bottomLeft;
                        break;
                    }
            }
        }
    }
}
