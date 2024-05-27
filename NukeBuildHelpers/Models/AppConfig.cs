namespace NukeBuildHelpers.Models;

public class AppConfig
{
    public required Dictionary<string, AppEntryConfig> AppEntryConfigs { get; init; }

    public required Dictionary<string, AppEntry> AppEntries { get; init; }

    public required Dictionary<string, AppTestEntry> AppTestEntries { get; init; }
}
