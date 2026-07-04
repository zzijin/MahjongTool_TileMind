using Microsoft.Extensions.Logging;
using TileMind.Common.Models;

namespace TileMind.Vision.ScreenCapture;

/// <summary>
/// 显示器信息查询服务。启动时通过 DXGI 枚举一次，缓存结果。
/// 列表按主显示器优先、虚拟桌面位置排序。
/// OutputIndex 对应 MonitorInfo.OutputIndex（DXGI 输出枚举索引，0-based）。
/// 注册为 Singleton。
/// </summary>
public class MonitorService
{
    private readonly List<MonitorInfo> _monitors;
    private readonly ILogger<MonitorService> _logger;

    /// <summary>排序后的显示器列表。</summary>
    public IReadOnlyList<MonitorInfo> All => _monitors;

    public MonitorService(ILogger<MonitorService> logger)
    {
        _logger = logger;
        _monitors = MonitorEnumerator.EnumerateAllStatic();

        if (_monitors.Count == 0)
            _logger.LogWarning("未枚举到任何显示器。");
        else
            _logger.LogInformation("已枚举 {Count} 个显示器: [{List}]",
                _monitors.Count,
                string.Join(", ", _monitors.Select(m =>
                    $"Out{m.OutputIndex}@{m.AdapterIndex} {m.Bounds.Width}x{m.Bounds.Height}{(m.IsPrimary ? " 主" : "")}")));
    }

    /// <summary>按 DXGI OutputIndex 查找（搜索所有适配器，返回首个匹配）。</summary>
    public MonitorInfo? FindByOutputIndex(int outputIndex)
        => _monitors.FirstOrDefault(m => m.OutputIndex == outputIndex);

    /// <summary>按 DXGI Adapter/Output 索引精确查找。</summary>
    public MonitorInfo? FindByOutput(int adapterIndex, int outputIndex)
        => _monitors.FirstOrDefault(m =>
            m.AdapterIndex == adapterIndex && m.OutputIndex == outputIndex);

    /// <summary>获取显示器边界矩形（物理像素）。</summary>
    public System.Drawing.RectangleF GetMonitorBounds(int outputIndex)
    {
        var m = FindByOutputIndex(outputIndex);
        return m?.Bounds ?? default;
    }
}
