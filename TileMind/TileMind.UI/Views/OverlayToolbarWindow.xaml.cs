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

        // 定位到左下角
        Loaded += (_, _) =>
        {
            var screen = SystemParameters.WorkArea;
            Left = 16;
            Top = screen.Bottom - Height - 16;
        };
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke();
    }
}
