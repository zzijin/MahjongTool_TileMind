using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using ZLogger;
using ZLogger.Providers;

namespace TileMind.Common.Logging
{
    public static class LoggingExtensions
    {
        public static ILoggingBuilder AddTileMindLogging(this ILoggingBuilder builder)
        {
            builder.ClearProviders();

            // 控制台日志 (JSON 格式)
            builder.AddZLoggerConsole(options =>
            {
                // 输出为 JSON 格式，便于日志系统解析
                options.UseJsonFormatter(formatter =>
                {
                    // 自定义 JSON 字段
                    //formatter.IncludeProperties = IncludeProperties.Timestamp |
                    //                              IncludeProperties.LogLevel |
                    //                              IncludeProperties.Message;
                    // 使用 UTC 时间
                    //formatter.UseUtcTimestamp = true;
                });
            });

            // 轮转文件日志 (按天/大小轮转)
            builder.AddZLoggerRollingFile(options =>
            {
                // 动态文件路径：Logs/TileMind-20260112-001.log
                options.FilePathSelector = (timestamp, sequenceNumber) =>
                {
                    var dateStr = timestamp.ToLocalTime().ToString("yyyyMMdd");
                    var seqStr = sequenceNumber.ToString("000") ?? "000";
                    return $"Logs/TileMind-{dateStr}-{seqStr}.log";
                };
                // 每天轮转
                options.RollingInterval = RollingInterval.Day;
                // 单个文件最大 50MB
                options.RollingSizeKB = 51200;
            });

            return builder;
        }
    }
}
