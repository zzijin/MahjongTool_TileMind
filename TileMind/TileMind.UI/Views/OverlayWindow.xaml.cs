using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using TileMind.UI.ViewModels;
using TileMind.Vision.ScreenCapture;

namespace TileMind.UI.Views;

public partial class OverlayWindow : Window
{
    private OverlayToolbarWindow? _toolbar;
    private double _targetLeft, _targetTop;

    public OverlayWindow(OverlayWindowViewModel viewModel, MonitorService monitorService)
    {
        DataContext = viewModel;

        var opts = viewModel.OverlayOptions;
        var monitor = monitorService.FindByOutputIndex(opts.OutputIndex);
        if (monitor != null)
        {
            _targetLeft = monitor.Bounds.X;
            _targetTop = monitor.Bounds.Y;
        }

        InitializeComponent();
        viewModel.SetOverlayControl(OverlayControl);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;

        SetWindowPos(hwnd, IntPtr.Zero, (int)_targetLeft, (int)_targetTop, 0, 0,
            SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        WindowState = WindowState.Maximized;

        var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);

        var dpi = VisualTreeHelper.GetDpi(this);
        var origin = PointFromScreen(new Point(0, 0));
        var matrix = new Matrix();
        matrix.Scale(1.0 / dpi.DpiScaleX, 1.0 / dpi.DpiScaleY);
        matrix.Translate(origin.X, origin.Y);
        OverlayControl.SetRenderTransform(matrix);
    }

    private void EnsureToolbar()
    {
        if (_toolbar == null || new WindowInteropHelper(_toolbar).Handle == IntPtr.Zero)
        {
            _toolbar?.Close();
            _toolbar = new OverlayToolbarWindow((OverlayWindowViewModel)DataContext);
            _toolbar.CloseRequested += () => Dispatcher.Invoke(() => { _toolbar?.Hide(); Hide(); });
            _toolbar.Owner = this;
            _toolbar.Show();
            return;
        }
        if (!_toolbar.IsVisible) _toolbar.Show();
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
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
}
