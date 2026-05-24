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
                options.UsePlainTextFormatter(formatter =>
                {
                    // 配置日志前缀：格式为 "时间戳|日志级别|"
                    formatter.SetPrefixFormatter(
                        $"{0:yyyy-MM-dd HH:mm:ss.fff}|{1:short}|{2}|",
                        (in MessageTemplate template, in LogInfo info) =>
                            template.Format(info.Timestamp, info.LogLevel, info.Category.Name)
                    );

                    // 配置日志后缀：格式为 " (记录器类别名)"
                    formatter.SetSuffixFormatter(
                        $" ({0})",
                        (in MessageTemplate template, in LogInfo info) =>
                            template.Format(info.Category)
                    );

                    // 自定义异常输出格式
                    formatter.SetExceptionFormatter(
                        (writer, ex) =>
                            Utf8StringInterpolation.Utf8String.Format(writer, $"异常: {ex.Message}")
                    );
                });
            });

            // 轮转文件日志 (按天/大小轮转)
            builder.AddZLoggerRollingFile(options =>
            {
                options.UsePlainTextFormatter(formatter =>
                {
                    // 配置日志前缀：格式为 "时间戳|日志级别|"
                    formatter.SetPrefixFormatter(
                        $"{0:yyyy-MM-dd HH:mm:ss.fff}|{1:short}|{2}|",
                        (in MessageTemplate template, in LogInfo info) =>
                            template.Format(info.Timestamp, info.LogLevel, info.Category.Name)
                    );

                    // 配置日志后缀：格式为 " (记录器类别名)"
                    formatter.SetSuffixFormatter(
                        $" ({0})",
                        (in MessageTemplate template, in LogInfo info) =>
                            template.Format(info.Category)
                    );

                    // 自定义异常输出格式
                    formatter.SetExceptionFormatter(
                        (writer, ex) =>
                            Utf8StringInterpolation.Utf8String.Format(writer, $"异常: {ex.Message}")
                    );
                });
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
