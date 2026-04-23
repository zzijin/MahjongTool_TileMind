using System;
using System.Collections.Generic;
using System.Text;
using TileMind.Common.Models;
using TileMind.UI.Overlay.OverlayBase;
using TileMind.UI.Overlay.OverlayBase.DrawingCommand;

namespace TileMind.UI.Overlay
{
    internal class MahjongTileDrawingInfo : DrawingInfo<IList<DetectionResult>>
    {
        public MahjongTileDrawingInfo(IList<DetectionResult> data, List<IDrawingCommand> drawingCommands) : base(data, drawingCommands)
        {

        }
    }
}
