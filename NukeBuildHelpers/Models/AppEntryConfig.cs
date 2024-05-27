namespace NukeBuildHelpers.Models;

public class AppEntryConfig
{
    public required AppEntry Entry { get; init; }

    public required List<AppTestEntry> Tests { get; init; }
}
