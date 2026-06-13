using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using TileMind.UI.ViewModels;

namespace TileMind.UI.Views;

public partial class OverlayWindow : Window
{
    private OverlayToolbarWindow? _toolbar;

    public OverlayWindow(OverlayWindowViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        OverlayControl.ItemsSource = viewModel.OverlayItems;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        // WS_EX_LAYERED: 支持透明窗口
        // WS_EX_TRANSPARENT: 鼠标穿透（所有鼠标事件传递到下层窗口）
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);

        // 将屏幕物理像素坐标映射到 WPF DIP 坐标（支持高 DPI）
        var dpi = VisualTreeHelper.GetDpi(this);
        var origin = PointFromScreen(new Point(0, 0)); // 屏幕原点在 WPF 客户区中的坐标
        var matrix = new Matrix();
        matrix.Scale(1.0 / dpi.DpiScaleX, 1.0 / dpi.DpiScaleY);
        matrix.Translate(origin.X, origin.Y);
        OverlayControl.SetRenderTransform(matrix);
    }

    private void EnsureToolbar()
    {
        // 工具栏已被销毁 → 重建
        if (_toolbar == null || new WindowInteropHelper(_toolbar).Handle == IntPtr.Zero)
        {
            _toolbar?.Close();
            _toolbar = new OverlayToolbarWindow((OverlayWindowViewModel)DataContext);
            // 直接在 Dispatcher 上同步执行 Hide，避免异步调度引发的时序问题（导致需要二次点击）
            _toolbar.CloseRequested += () => Dispatcher.Invoke(() => { _toolbar?.Hide(); Hide(); });
            _toolbar.Owner = this;
            _toolbar.Show();
            return;
        }

        // 工具栏只是隐藏 → 重新显示
        if (!_toolbar.IsVisible)
            _toolbar.Show();
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        EnsureToolbar();
    }

    protected override void OnClosed(EventArgs e)
    {
        _toolbar?.Close();
        base.OnClosed(e);
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TRANSPARENT = 0x20;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
}
