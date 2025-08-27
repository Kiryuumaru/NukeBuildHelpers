using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.Pipelines.Common.Enums;

namespace NukeBuildHelpers.RunContext.Interfaces;

/// <summary>
/// Represents a unified context that includes all run information.
/// </summary>
public interface IRunContext
{
    /// <summary>
    /// Gets the run type associated with the context.
    /// </summary>
    RunType RunType { get; }

    /// <summary>
    /// Gets the pipeline type associated with the context.
    /// Only set for pipeline contexts, null for local contexts.
    /// </summary>
    PipelineType? PipelineType { get; }

    /// <summary>
    /// Gets the application version associated with the context.
    /// Only set for versioned contexts, null for commit-only or local contexts.
    /// </summary>
    AppVersion? AppVersion { get; }

    /// <summary>
    /// Gets the bump release version associated with the context.
    /// Only set for bump contexts, null for other types.
    /// </summary>
    BumpReleaseVersion? BumpReleaseVersion { get; }

    /// <summary>
    /// Gets the pull request release version associated with the context.
    /// Only set for pull request contexts, null for other types.
    /// </summary>
    PullRequestReleaseVersion? PullRequestReleaseVersion { get; }
}
