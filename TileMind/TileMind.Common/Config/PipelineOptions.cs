namespace TileMind.Common.Config;

/// <summary>
/// Pipeline behavior options.
/// </summary>
public class PipelineOptions
{
    public const string SettingFilePath = @".\settings\pipelinesettings.json";

    /// <summary>Enable cross-frame game state tracking. When false, only static analysis runs.</summary>
    public bool EnableStateTracking { get; set; } = true;
}
