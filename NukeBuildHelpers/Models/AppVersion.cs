using Semver;

namespace NukeBuildHelpers;

public class AppVersion
{
    public required string AppId { get; init; }

    public required string Environment { get; init; }

    public required SemVersion Version { get; init; }

    public required long BuildId { get; init; }

    public required string ReleaseNotes { get; init; }
}
