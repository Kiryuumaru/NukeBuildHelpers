using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.RunContext.Models;

namespace NukeBuildHelpers.RunContext.Interfaces;

internal class PullRequestContext : VersionedContext<PullRequestReleaseVersion>, IPullRequestContext
{
    PullRequestReleaseVersion IPullRequestContext.AppVersion { get => AppVersion; }
}
