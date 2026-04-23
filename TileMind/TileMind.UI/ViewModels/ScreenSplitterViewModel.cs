using System;
using System.Collections.Generic;
using System.Text;
using TileMind.Common.Config;

namespace TileMind.UI.ViewModels
{
    public class ScreenSplitterViewModel : ViewModel
    {
        ScreenCaptureOptions _options;
        public ScreenSplitterViewModel(ScreenCaptureOptions options)
        {
            _options = options;
        }
    }
}
