using System.Drawing;
using Microsoft.Extensions.Logging;
using SharpDX.DXGI;
using TileMind.Common.Models;

namespace TileMind.Vision.ScreenCapture;

/// <summary>
/// 通过 SharpDX DXGI 枚举所有显示器输出。
/// UserIndex 按主显示器优先、虚拟桌面位置排列（后续可通过 UI 提供每屏标识）。
/// </summary>
public partial class MonitorEnumerator
{
    private readonly ILogger _logger;

    public MonitorEnumerator(ILogger logger)
    {
        _logger = logger;
    }

    public List<MonitorInfo> EnumerateAll()
    {
        var monitors = EnumerateCore();
        _logger.LogInformation("已枚举 {Count} 个显示器: [{List}]",
            monitors.Count,
            string.Join(", ", monitors.Select(m =>
                $"Out{m.OutputIndex}@{m.AdapterIndex} {m.DeviceName} {m.Bounds.Width}x{m.Bounds.Height}{(m.IsPrimary ? " 主" : "")}")));
        return monitors;
    }

    // ── 静态缓存 ──

    private static List<MonitorInfo>? _cached;

    public static List<MonitorInfo> EnumerateAllStatic()
    {
        if (_cached != null) return _cached;
        _cached = EnumerateCore();
        return _cached;
    }

    // ── 核心枚举 ──

    private static List<MonitorInfo> EnumerateCore()
    {
        var monitors = new List<MonitorInfo>();

        try
        {
            using var factory = new Factory1();

            for (int a = 0; a < factory.GetAdapterCount(); a++)
            {
                using var adapter = factory.GetAdapter1(a);
                for (int o = 0; o < adapter.GetOutputCount(); o++)
                {
                    using var output = adapter.GetOutput(o);
                    var desc = output.Description;
                    if (!desc.IsAttachedToDesktop)
                        continue;

                    var bounds = desc.DesktopBounds;
                    // 主显示器在虚拟桌面的原点 (0,0)
                    bool isPrimary = bounds.Left == 0 && bounds.Top == 0;

                    monitors.Add(new MonitorInfo
                    {
                        DeviceName = desc.DeviceName,
                        AdapterIndex = a,
                        OutputIndex = o,
                        Bounds = new RectangleF(
                            bounds.Left, bounds.Top,
                            bounds.Right - bounds.Left,
                            bounds.Bottom - bounds.Top),
                        IsPrimary = isPrimary,
                        MonitorHandle = desc.MonitorHandle,
                    });
                }
            }
        }
        catch { /* 调用方处理空结果 */ }

        // 主显示器排第一，其余按虚拟桌面位置
        monitors.Sort((a, b) =>
        {
            if (a.IsPrimary != b.IsPrimary)
                return a.IsPrimary ? -1 : 1;
            int yCmp = a.Bounds.Y.CompareTo(b.Bounds.Y);
            if (yCmp != 0) return yCmp;
            return a.Bounds.X.CompareTo(b.Bounds.X);
        });

        return monitors;
    }
}
