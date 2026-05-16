namespace TileMind.Common.Models;

/// <summary>
/// 帧状态事件中枢（Singleton）。
/// GamePipelineService 每帧处理后发布，OverlayWindowViewModel 订阅。
/// </summary>
public class FrameStateHub
{
    public event Action<FrameDetections, List<MahjongAction>>? FrameProcessed;

    public void Publish(FrameDetections detections, List<MahjongAction> actions)
    {
        FrameProcessed?.Invoke(detections, actions);
    }
}
