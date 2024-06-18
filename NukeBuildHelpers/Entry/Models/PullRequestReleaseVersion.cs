using Semver;

namespace NukeBuildHelpers.Entry.Models;

public class PullRequestReleaseVersion : AppVersion
{
    public required long PullRequestNumber { get; init; }
}
