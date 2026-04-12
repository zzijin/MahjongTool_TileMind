using System;
using System.Collections.Generic;
using System.Text;

namespace TileMind.Common.Config
{
    public class ScreenCaptureOptions
    {

        //DXGI 适配器索引，通常0表示主显卡
        public int AdapterIndex { get; set; } = 0;
        //显示器索引，通常0表示主显示器
        public int OutputIndex { get; set; } = 0;
    }
}
