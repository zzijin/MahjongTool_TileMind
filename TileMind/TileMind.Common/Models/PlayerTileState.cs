using System;
using System.Collections.Generic;
using System.Text;

namespace TileMind.Common.Models
{
    public class PlayerTileState
    {
        public PlayerTileState()
        {
            HandTiles = new List<DetectionResult>();
            DiscardPondTiles = new List<DetectionResult>();
            ChiTiles = new List<DetectionResult>();
            PonTiles = new List<DetectionResult>();
            KanTiles = new List<DetectionResult>();
            AnkanTiles = new List<DetectionResult>();
        }

        //手牌状态
        public List<DetectionResult> HandTiles { get; }
        //牌河
        public List<DetectionResult> DiscardPondTiles { get; }
        //吃
        public List<DetectionResult> ChiTiles { get; }
        //碰
        public List<DetectionResult> PonTiles { get; }
        //明杠
        public List<DetectionResult> KanTiles { get; }
        //暗杠
        public List<DetectionResult> AnkanTiles { get; }
        //副露
        public IEnumerable<DetectionResult> MeldTiles => ChiTiles.Concat(PonTiles).Concat(KanTiles);
    }
}
