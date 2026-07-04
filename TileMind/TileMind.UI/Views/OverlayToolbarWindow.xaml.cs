using System.Windows;
using TileMind.UI.ViewModels;

namespace TileMind.UI.Views;

public partial class OverlayToolbarWindow : Window
{
    public event Action? CloseRequested;

    public OverlayToolbarWindow(OverlayWindowViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();

        // 定位到 Owner 窗口（OverlayWindow）的左下角，自动跟随到正确显示器
        Loaded += (_, _) =>
        {
            if (Owner != null)
            {
                Left = Owner.Left + 16;
                Top = Owner.Top + Owner.ActualHeight - Height - 16;
            }
        };
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke();
    }
}
