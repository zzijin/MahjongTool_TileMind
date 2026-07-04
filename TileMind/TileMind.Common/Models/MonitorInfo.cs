using System.Drawing;

namespace TileMind.Common.Models;

/// <summary>
/// 显示器信息。OutputIndex 对应 DXGI 适配器上的输出枚举索引（0-based），
/// 与 AdapterIndex 组合可唯一标识一个显示器输出。
/// 列表按主显示器优先、虚拟桌面位置排序。
/// </summary>
public class MonitorInfo
{
    /// <summary>GDI 设备名，如 \\.\DISPLAY1。</summary>
    public string DeviceName { get; init; } = string.Empty;

    /// <summary>所在 DXGI 适配器索引。</summary>
    public int AdapterIndex { get; init; }

    /// <summary>在适配器上的 DXGI Output 枚举索引（0-based）。</summary>
    public int OutputIndex { get; init; }

    /// <summary>桌面边界矩形（虚拟屏幕坐标，物理像素）。</summary>
    public RectangleF Bounds { get; init; }

    /// <summary>是否为主显示器。</summary>
    public bool IsPrimary { get; init; }

    /// <summary>显示器句柄（HMONITOR）。</summary>
    public IntPtr MonitorHandle { get; init; }
}

/// <summary>MonitorInfo 列表的扩展查询方法。</summary>
public static class MonitorInfoExtensions
{
    /// <summary>按 OutputIndex（DXGI 输出索引）查找，返回首个匹配。</summary>
    public static MonitorInfo? FindByOutputIndex(this List<MonitorInfo> monitors, int outputIndex)
        => monitors.FirstOrDefault(m => m.OutputIndex == outputIndex);
}
