using System;
using System.Collections.Generic;
using System.Text;

namespace TileMind.Common.Models
{
    public class PlayerTileState
    {
        //手牌状态
        public List<TileDetectionResult> HandTiles { get; }
        //牌河
        public List<TileDetectionResult> DiscardPondTiles { get; }
        //副露
        public List<TileDetectionResult> MeldTiles { get; }
        //暗杠
        public List<TileDetectionResult> AnkanTiles { get; }
    }
}
