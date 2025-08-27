using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.RunContext.Interfaces;
using System.Diagnostics.CodeAnalysis;

namespace NukeBuildHelpers.RunContext.Extensions;

/// <summary>
/// Provides extension methods for handling run contexts.
/// </summary>
public static class RunContextExtensions
{
    /// <summary>
    /// Tries to get bump context information from the run context.
    /// </summary>
    /// <param name="runContext">The current run context.</param>
    /// <param name="bumpVersion">The bump release version if this is a bump run.</param>
    /// <returns>True if this is a bump run; otherwise, false.</returns>
    public static bool TryGetBumpContext(this IRunContext runContext, [NotNullWhen(true)] out BumpReleaseVersion? bumpVersion)
    {
        bumpVersion = runContext.BumpVersion;
        return bumpVersion != null;
    }

    /// <summary>
    /// Tries to get pull request context information from the run context.
    /// </summary>
    /// <param name="runContext">The current run context.</param>
    /// <param name="pullRequestVersion">The pull request version if this is a PR run.</param>
    /// <returns>True if this is a pull request run; otherwise, false.</returns>
    public static bool TryGetPullRequestContext(this IRunContext runContext, [NotNullWhen(true)] out PullRequestReleaseVersion? pullRequestVersion)
    {
        pullRequestVersion = runContext.PullRequestVersion;
        return pullRequestVersion != null;
    }

    /// <summary>
    /// Checks if this is a local development run.
    /// </summary>
    /// <param name="runContext">The current run context.</param>
    /// <returns>True if this is a local run; otherwise, false.</returns>
    public static bool IsLocalContext(this IRunContext runContext)
    {
        return runContext.IsLocal;
    }

    /// <summary>
    /// Checks if this is a pipeline run.
    /// </summary>
    /// <param name="runContext">The current run context.</param>
    /// <returns>True if this is a pipeline run; otherwise, false.</returns>
    public static bool IsPipelineContext(this IRunContext runContext)
    {
        return runContext.IsPipeline;
    }

    /// <summary>
    /// Checks if this is a commit run.
    /// </summary>
    /// <param name="runContext">The current run context.</param>
    /// <returns>True if this is a commit run; otherwise, false.</returns>
    public static bool IsCommitContext(this IRunContext runContext)
    {
        return runContext.IsCommit;
    }

    /// <summary>
    /// Checks if this run has version information.
    /// </summary>
    /// <param name="runContext">The current run context.</param>
    /// <returns>True if this run has version information; otherwise, false.</returns>
    public static bool IsVersionedContext(this IRunContext runContext)
    {
        return runContext.IsVersioned;
    }
}
