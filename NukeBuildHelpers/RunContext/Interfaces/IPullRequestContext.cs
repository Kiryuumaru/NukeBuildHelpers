using NukeBuildHelpers.Entry.Models;

namespace NukeBuildHelpers.RunContext.Interfaces;

/// <summary>
/// Represents a context that includes information specific to pull requests.
/// </summary>
public interface IPullRequestContext : IVersionedContext
{
    /// <summary>
    /// Gets the pull request release version associated with the context.
    /// </summary>
    new PullRequestReleaseVersion AppVersion { get; }
}
