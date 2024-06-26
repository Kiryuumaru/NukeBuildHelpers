using NukeBuildHelpers.Entry.Models;

namespace NukeBuildHelpers.RunContext.Models;

public interface IPullRequestContext : IVersionedContext
{
    new PullRequestReleaseVersion AppVersion { get; }
}
