using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using TileMind.Common.Logging;
using TileMind.UI.ViewModels;
using TileMind.UI.Views;
using TileMind.Vision.Detection;
using TileMind.Vision.ScreenCapture;

namespace TileMind.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IServiceProvider _serviceProvider;

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
            services.AddLogging(builder => builder.AddTileMindLogging());

            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                // 视觉配置文件，当配置发生变化时自动重新加载配置
                .AddJsonFile("visionsettings.json", optional: true, reloadOnChange: true)
                .Build();
            services.AddSingleton<IConfiguration>(config);

            //注册UI服务
            services.AddSingleton<MainWindow>();
            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<OverlayWindow>();
            services.AddSingleton<OverlayWindowViewModel>();


            //注册公共服务
            services.AddSingleton<ILogger>();

            //注册视觉服务
            services.AddScoped<YoloDetectorPoolService>();
            services.AddScoped<IScreenCaptureService, DxgiScreenCaptureService>();
            services.AddScoped<FrameFusionService>();

            //注册AI服务
        }
    }

}
