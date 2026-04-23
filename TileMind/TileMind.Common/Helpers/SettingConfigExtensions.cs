using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TileMind.Common.Config;

namespace TileMind.Common.Helpers
{
    public static class SettingConfigExtensions
    {
        extension(FrameFusionOptions options)
        {
            public void Save(string filePath = FrameFusionOptions.SettingFilePath)
            {
                using FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                using var writer = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = true });

                writer.WriteStartObject();
                writer.WritePropertyName("Yolo");
                JsonSerializer.Serialize(writer, options, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
                writer.WriteEndObject();
                writer.Flush();
            }
        }
        extension(ScreenCaptureOptions options)
        {
            public void Save(string filePath = ScreenCaptureOptions.SettingFilePath)
            {
                using FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                using var writer = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = true });

                writer.WriteStartObject();
                writer.WritePropertyName("Yolo");
                JsonSerializer.Serialize(writer, options, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
                writer.WriteEndObject();
                writer.Flush();
            }
        }
        extension(YoloOptions options)
        {
            public void Save(string filePath = YoloOptions.SettingFilePath)
            {
                using FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                using var writer = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = true });

                writer.WriteStartObject();
                writer.WritePropertyName("Yolo");
                JsonSerializer.Serialize(writer, options, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
                writer.WriteEndObject();
                writer.Flush();
            }
        }
    }
}
