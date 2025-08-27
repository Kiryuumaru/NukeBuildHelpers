using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.Pipelines.Common.Enums;

namespace NukeBuildHelpers.RunContext.Interfaces;

/// <summary>
/// Represents a context that includes run type information.
/// </summary>
public interface IRunContext
{
    /// <summary>
    /// Gets the run type associated with the context.
    /// </summary>
    RunType RunType { get; }

    /// <summary>
    /// Gets the pipeline type associated with the context.
    /// </summary>
    PipelineType PipelineType { get; }

    /// <summary>
    /// Gets the application version associated with the context.
    /// </summary>
    AppVersion AppVersion { get; }

    /// <summary>
    /// Gets the bump release version with release notes. Null unless this is a bump run.
    /// </summary>
    BumpReleaseVersion? BumpVersion { get; }

    /// <summary>
    /// Gets the pull request release version with PR number. Null unless this is a pull request run.
    /// </summary>
    PullRequestReleaseVersion? PullRequestVersion { get; }

    /// <summary>
    /// Gets whether this is a local development run.
    /// </summary>
    bool IsLocal => RunType == RunType.Local;

    /// <summary>
    /// Gets whether this is a commit-based run.
    /// </summary>
    bool IsCommit => RunType == RunType.Commit;

    /// <summary>
    /// Gets whether this is a version bump run.
    /// </summary>
    bool IsBump => BumpVersion != null;

    /// <summary>
    /// Gets whether this is a pull request run.
    /// </summary>
    bool IsPullRequest => PullRequestVersion != null;

    /// <summary>
    /// Gets whether this run has version information.
    /// </summary>
    bool IsVersioned => AppVersion != null;
}
