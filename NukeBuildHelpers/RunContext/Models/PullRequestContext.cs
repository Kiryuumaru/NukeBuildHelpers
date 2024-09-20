using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.RunContext.Interfaces;

namespace NukeBuildHelpers.RunContext.Models;

internal class PullRequestContext : VersionedContext<PullRequestReleaseVersion>, IPullRequestContext
{
    PullRequestReleaseVersion IPullRequestContext.AppVersion { get => AppVersion; }
}
