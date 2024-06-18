using Semver;

namespace NukeBuildHelpers.Entry.Models;

internal class AllVersions
{
    public required Dictionary<string, List<long>> CommitBuildIdGrouped { get; init; }

    public required Dictionary<string, List<SemVersion>> CommitVersionGrouped { get; init; }

    public required Dictionary<string, List<string>> CommitLatestTagGrouped { get; init; }

    public required Dictionary<SemVersion, string> VersionCommitPaired { get; init; }

    public required Dictionary<long, string> BuildIdCommitPaired { get; init; }

    public required Dictionary<string, List<SemVersion>> EnvVersionGrouped { get; init; }

    public required Dictionary<string, List<long>> EnvBuildIdGrouped { get; init; }

    public required Dictionary<string, SemVersion> EnvLatestVersionPaired { get; init; }

    public required Dictionary<string, long> EnvLatestBuildIdPaired { get; init; }

    public required List<string> EnvSorted { get; init; }

    public required List<SemVersion> VersionBump { get; init; }

    public required List<SemVersion> VersionQueue { get; init; }

    public required List<SemVersion> VersionFailed { get; init; }

    public required List<SemVersion> VersionPassed { get; init; }
}
