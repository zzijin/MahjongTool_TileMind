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
        private readonly INavigationViewPageProvider _pageProvider;
        public MainWindowViewModel ViewModel { get; }

        public MainWindow(MainWindowViewModel viewModel, INavigationViewPageProvider pageProvider)
        {
            ViewModel = viewModel;
            _pageProvider = pageProvider;
            DataContext = this;

            InitializeComponent();

            SetPageService(_pageProvider);
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