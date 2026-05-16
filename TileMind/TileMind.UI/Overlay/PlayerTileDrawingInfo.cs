using TileMind.Common.Models;
using TileMind.UI.Overlay.OverlayBase;
using TileMind.UI.Overlay.OverlayBase.DrawingCommand;

namespace TileMind.UI.Overlay;

/// <summary>
/// 携带玩家座位信息的绘制数据，用于按玩家着色。
/// </summary>
public class PlayerTileDrawingInfo : DrawingInfo<IList<DetectionResult>>
{
    public SeatPosition Seat { get; }

    public PlayerTileDrawingInfo(SeatPosition seat, IList<DetectionResult> data, List<IDrawingCommand> commands)
        : base(data, commands)
    {
        Seat = seat;
    }
}
