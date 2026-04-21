using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TileMind.UI.ViewModels;
using TileMind.UI.Views;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;

namespace TileMind.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : FluentWindow, INavigationWindow
    {
        private Window? overlayWindow;
        private readonly INavigationViewPageProvider _pageProvider;
        public MainWindowViewModel ViewModel { get; }

        public MainWindow(MainWindowViewModel viewModel, INavigationViewPageProvider pageProvider)
        {
            ViewModel = viewModel;
            _pageProvider = pageProvider;
            DataContext = ViewModel;

            InitializeComponent();

            // 将 NavigationView 控件与窗口关联（用于导航服务）
            SetPageService(_pageProvider);
        }

        private void OpenOverlay_Click(object sender, RoutedEventArgs e)
        {
            if (overlayWindow == null)
            {
                overlayWindow = new OverlayWindow();
            }
            overlayWindow.Show();
        }

        public INavigationView GetNavigation() => RootNavigation;

        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);


        public void SetPageService(INavigationViewPageProvider navigationViewPageProvider) =>
                RootNavigation.SetPageProviderService(navigationViewPageProvider);

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            throw new NotImplementedException();
        }

        public void ShowWindow() => Show();

        public void CloseWindow() => Close();

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Make sure that closing this window will begin the process of closing the application.
            Application.Current.Shutdown();
        }
    }
}