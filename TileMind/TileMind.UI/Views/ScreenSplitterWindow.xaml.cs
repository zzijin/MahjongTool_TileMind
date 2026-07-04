using System.Runtime.InteropServices;
using System.Windows;
using TileMind.UI.ViewModels;

namespace TileMind.UI.Views;

public partial class ScreenSplitterWindow : Window
{
    private readonly ScreenSplitterOverlayControl _splitterControl;
    private readonly ScreenSplitterViewModel _viewModel;
    private double _targetLeft, _targetTop;

    public ScreenSplitterWindow(ScreenSplitterOverlayControl splitterControl, ScreenSplitterViewModel viewModel)
    {
        _splitterControl = splitterControl;
        _viewModel = viewModel;

        // 记录目标位置，由 OnSourceInitialized 实际定位
        var monitor = viewModel.GetTargetMonitor();
        if (monitor != null)
        {
            _targetLeft = monitor.Bounds.X;
            _targetTop = monitor.Bounds.Y;
        }

        InitializeComponent();

        // 单例控件可能还挂在上一个窗口上，先断开再插入
        if (_splitterControl.Parent is System.Windows.Controls.Panel parent)
            parent.Children.Remove(_splitterControl);
        RootGrid.Children.Insert(0, _splitterControl);

        _viewModel.SetControl(_splitterControl);

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // 先移动到目标显示器，再最大化
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        SetWindowPos(hwnd, IntPtr.Zero,
            (int)_targetLeft, (int)_targetTop, 0, 0,
            SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        WindowState = WindowState.Maximized;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.LoadConfig();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SaveConfigCommand.Execute(null);
        MessageBox.Show("区域配置已保存。", "TileMind", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RelocateButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.RelocateCommand.Execute(null);
    }

    private void ResetDefaultButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ResetToDefaultCommand.Execute(null);
        MessageBox.Show("Ratio 已恢复为默认值并保存。", "TileMind", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        RootGrid.Children.Remove(_splitterControl);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);
}
