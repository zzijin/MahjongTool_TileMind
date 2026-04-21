using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using TileMind.Common.Logging;
using TileMind.Vision.Detection;
using TileMind.Vision.ScreenCapture;

namespace TileMind.Core.Services
{
    public static class ServiceExtensions
    {

        extension(IServiceCollection services)
        {
            public void AddBaseServices()
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    // 视觉配置文件，当配置发生变化时自动重新加载配置
                    .AddJsonFile("visionsettings.json", optional: true, reloadOnChange: true)
                    .Build();
                services.AddSingleton<IConfiguration>(config);

                //注册公共服务
                services.AddLogging(builder => builder.AddTileMindLogging());

                //注册视觉服务
                services.AddScoped<YoloDetectorPoolService>();
                services.AddScoped<IScreenCaptureService, DxgiScreenCaptureService>();
                services.AddScoped<FrameFusionService>();

                //注册AI服务
            }
        }
    }
}
