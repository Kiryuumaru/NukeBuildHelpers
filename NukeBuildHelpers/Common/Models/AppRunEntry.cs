namespace NukeBuildHelpers.Common.Models;

internal class AppRunEntry
{
    public required string AppId { get; init; }

    public required string Environment { get; init; }

    public required string Version { get; init; }

    public required bool HasRelease { get; init; }
}
