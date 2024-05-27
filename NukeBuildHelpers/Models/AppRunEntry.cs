using Semver;

namespace NukeBuildHelpers.Models;

internal class AppRunEntry
{
    public required AppEntry AppEntry { get; init; }

    public required string Env { get; init; }

    public required SemVersion Version { get; init; }

    public required bool HasRelease { get; init; }
}
