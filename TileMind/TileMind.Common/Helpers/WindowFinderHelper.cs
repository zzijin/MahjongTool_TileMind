using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace TileMind.Common.Helpers;

/// <summary>
/// 通过进程名查找游戏窗口，并获取客户区屏幕坐标。
/// </summary>
public static class WindowFinderHelper
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left, top, right, bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x, y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    // --- EnumDisplayMonitors ---

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip,
        MonitorEnumDelegate lpfnEnum, IntPtr dwData);

    private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdc, ref RECT lprcMonitor, IntPtr dwData);

    /// <summary>
    /// 获取指定索引显示器的边界矩形（相对于虚拟屏幕左上角）。
    /// 索引无效时回退到 SM_CXSCREEN/SM_CYSCREEN（主显示器）。
    /// </summary>
    public static RectangleF GetMonitorBounds(int monitorIndex)
    {
        var monitors = new List<RECT>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdc, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                monitors.Add(lprcMonitor);
                return true;
            }, IntPtr.Zero);

        if (monitorIndex >= 0 && monitorIndex < monitors.Count)
        {
            var r = monitors[monitorIndex];
            return new RectangleF(r.left, r.top, r.right - r.left, r.bottom - r.top);
        }

        // 回退：主显示器尺寸
        int sw = GetSystemMetrics(0);  // SM_CXSCREEN
        int sh = GetSystemMetrics(1);  // SM_CYSCREEN
        return new RectangleF(0, 0, sw, sh);
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    /// <summary>
    /// 根据进程名查找主窗口的客户区屏幕矩形。
    /// </summary>
    /// <param name="processName">进程名（不含 .exe）</param>
    /// <returns>客户区屏幕矩形，未找到或窗口最小化时返回 null</returns>
    public static RectangleF? FindClientRect(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return null;

        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(processName);
        }
        catch
        {
            return null;
        }

        if (processes.Length == 0)
            return null;

        // 取第一个，不做多开处理
        var hwnd = processes[0].MainWindowHandle;
        processes[0].Dispose();

        if (hwnd == IntPtr.Zero)
            return null;

        if (!GetClientRect(hwnd, out RECT clientRect))
            return null;

        // 窗口最小化时客户区尺寸为 0
        int width = clientRect.right - clientRect.left;
        int height = clientRect.bottom - clientRect.top;
        if (width <= 0 || height <= 0)
            return null;

        var pt = new POINT { x = 0, y = 0 };
        if (!ClientToScreen(hwnd, ref pt))
            return null;

        return new RectangleF(pt.x, pt.y, width, height);
    }
}
