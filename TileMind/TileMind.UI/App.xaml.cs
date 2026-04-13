using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using TileMind.Common.Logging;
using TileMind.UI.ViewModel;
using TileMind.UI.View;
using TileMind.Vision.Detection;
using TileMind.Vision.ScreenCapture;
using TileMind.Core.Services;

namespace TileMind.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IServiceProvider? _serviceProvider;

        protected override void OnStartup(StartupEventArgs e)
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddBaseServices();

            //注册UI服务
            services.AddSingleton<MainWindow>();
            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<OverlayWindow>();
            services.AddSingleton<OverlayWindowViewModel>();
        }
    }

}
