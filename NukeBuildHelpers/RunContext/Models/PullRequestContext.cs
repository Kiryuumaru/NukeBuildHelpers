using NukeBuildHelpers.Entry.Models;

namespace NukeBuildHelpers.RunContext.Interfaces;

internal class PullRequestContext : VersionedContext<PullRequestReleaseVersion>, IPullRequestContext
{
    PullRequestReleaseVersion IPullRequestContext.AppVersion { get => AppVersion; }
}
