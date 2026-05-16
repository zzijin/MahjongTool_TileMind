using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using TileMind.UI.Views;
using Wpf.Ui;

namespace TileMind.UI.ViewModels;

public partial class HomeViewModel : ViewModel
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Window _mainWindow;

    public HomeViewModel(IServiceProvider serviceProvider, INavigationWindow navigationWindow)
    {
        _serviceProvider = serviceProvider;
        _mainWindow = (Window)navigationWindow;
    }

    [RelayCommand]
    private void OpenScreenSplitter()
    {
        var window = _serviceProvider.GetRequiredService<ScreenSplitterWindow>();
        window.Owner = _mainWindow;

        _mainWindow.WindowState = WindowState.Minimized;
        window.ShowDialog();
        _mainWindow.WindowState = WindowState.Normal;
    }

    [RelayCommand]
    private void OpenOverlay()
    {
        var window = _serviceProvider.GetRequiredService<OverlayWindow>();
        window.Show();
        _mainWindow.WindowState = WindowState.Minimized;
    }
}
