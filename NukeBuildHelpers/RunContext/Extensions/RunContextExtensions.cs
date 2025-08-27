using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.RunContext.Interfaces;
using System.Diagnostics.CodeAnalysis;

namespace NukeBuildHelpers.RunContext.Extensions;

/// <summary>
/// Provides extension methods for handling different run contexts.
/// </summary>
public static class RunContextExtensions
{
    /// <summary>
    /// Tries to get a bump release version from the provided run context.
    /// </summary>
    /// <param name="runContext">The current run context.</param>
    /// <param name="bumpReleaseVersion">The bump release version if found.</param>
    /// <returns>True if the bump release version is found; otherwise, false.</returns>
    public static bool TryGetBumpReleaseVersion(this IRunContext runContext, [NotNullWhen(true)] out BumpReleaseVersion? bumpReleaseVersion)
    {
        bumpReleaseVersion = runContext.BumpReleaseVersion;
        return bumpReleaseVersion != null;
    }

    /// <summary>
    /// Tries to get a pull request release version from the provided run context.
    /// </summary>
    /// <param name="runContext">The current run context.</param>
    /// <param name="pullRequestReleaseVersion">The pull request release version if found.</param>
    /// <returns>True if the pull request release version is found; otherwise, false.</returns>
    public static bool TryGetPullRequestReleaseVersion(this IRunContext runContext, [NotNullWhen(true)] out PullRequestReleaseVersion? pullRequestReleaseVersion)
    {
        pullRequestReleaseVersion = runContext.PullRequestReleaseVersion;
        return pullRequestReleaseVersion != null;
    }

    /// <summary>
    /// Tries to get an application version from the provided run context.
    /// </summary>
    /// <param name="runContext">The current run context.</param>
    /// <param name="appVersion">The application version if found.</param>
    /// <returns>True if the application version is found; otherwise, false.</returns>
    public static bool TryGetAppVersion(this IRunContext runContext, [NotNullWhen(true)] out AppVersion? appVersion)
    {
        appVersion = runContext.AppVersion;
        return appVersion != null;
    }

    /// <summary>
    /// Checks if the run context is a local context.
    /// </summary>
    /// <param name="runContext">The current run context.</param>
    /// <returns>True if the context is a local context; otherwise, false.</returns>
    public static bool IsLocalContext(this IRunContext runContext)
    {
        return runContext.PipelineType == null;
    }

    /// <summary>
    /// Checks if the run context is a pipeline context.
    /// </summary>
    /// <param name="runContext">The current run context.</param>
    /// <returns>True if the context is a pipeline context; otherwise, false.</returns>
    public static bool IsPipelineContext(this IRunContext runContext)
    {
        return runContext.PipelineType != null;
    }

    /// <summary>
    /// Checks if the run context is a versioned context.
    /// </summary>
    /// <param name="runContext">The current run context.</param>
    /// <returns>True if the context is a versioned context; otherwise, false.</returns>
    public static bool IsVersionedContext(this IRunContext runContext)
    {
        return runContext.AppVersion != null;
    }

    /// <summary>
    /// Checks if the run context is a bump context.
    /// </summary>
    /// <param name="runContext">The current run context.</param>
    /// <returns>True if the context is a bump context; otherwise, false.</returns>
    public static bool IsBumpContext(this IRunContext runContext)
    {
        return runContext.BumpReleaseVersion != null;
    }

    /// <summary>
    /// Checks if the run context is a pull request context.
    /// </summary>
    /// <param name="runContext">The current run context.</param>
    /// <returns>True if the context is a pull request context; otherwise, false.</returns>
    public static bool IsPullRequestContext(this IRunContext runContext)
    {
        return runContext.PullRequestReleaseVersion != null;
    }

    // Legacy compatibility methods (to ease migration):

    /// <summary>
    /// Legacy method for backward compatibility. Use TryGetBumpReleaseVersion instead.
    /// </summary>
    /// <param name="runContext">The current run context.</param>
    /// <param name="bumpContext">A wrapper object that provides access to the bump release version.</param>
    /// <returns>True if the bump context is found; otherwise, false.</returns>
    [Obsolete("Use TryGetBumpReleaseVersion instead or check IsBumpContext() and access BumpReleaseVersion directly")]
    public static bool TryGetBumpContext(this IRunContext runContext, [NotNullWhen(true)] out IBumpContextWrapper? bumpContext)
    {
        if (runContext.BumpReleaseVersion != null)
        {
            bumpContext = new BumpContextWrapper(runContext.BumpReleaseVersion);
            return true;
        }
        bumpContext = null;
        return false;
    }

    /// <summary>
    /// Legacy method for backward compatibility. Use TryGetPullRequestReleaseVersion instead.
    /// </summary>
    /// <param name="runContext">The current run context.</param>
    /// <param name="pullRequestContext">A wrapper object that provides access to the pull request release version.</param>
    /// <returns>True if the pull request context is found; otherwise, false.</returns>
    [Obsolete("Use TryGetPullRequestReleaseVersion instead or check IsPullRequestContext() and access PullRequestReleaseVersion directly")]
    public static bool TryGetPullRequestContext(this IRunContext runContext, [NotNullWhen(true)] out IPullRequestContextWrapper? pullRequestContext)
    {
        if (runContext.PullRequestReleaseVersion != null)
        {
            pullRequestContext = new PullRequestContextWrapper(runContext.PullRequestReleaseVersion);
            return true;
        }
        pullRequestContext = null;
        return false;
    }
}

/// <summary>
/// Wrapper interface for backward compatibility with IBumpContext.
/// </summary>
public interface IBumpContextWrapper
{
    /// <summary>
    /// Gets the bump release version.
    /// </summary>
    BumpReleaseVersion AppVersion { get; }
}

/// <summary>
/// Wrapper interface for backward compatibility with IPullRequestContext.
/// </summary>
public interface IPullRequestContextWrapper
{
    /// <summary>
    /// Gets the pull request release version.
    /// </summary>
    PullRequestReleaseVersion AppVersion { get; }
}

/// <summary>
/// Internal wrapper implementation for bump context backward compatibility.
/// </summary>
internal class BumpContextWrapper : IBumpContextWrapper
{
    public BumpReleaseVersion AppVersion { get; }

    public BumpContextWrapper(BumpReleaseVersion appVersion)
    {
        AppVersion = appVersion;
    }
}

/// <summary>
/// Internal wrapper implementation for pull request context backward compatibility.
/// </summary>
internal class PullRequestContextWrapper : IPullRequestContextWrapper
{
    public PullRequestReleaseVersion AppVersion { get; }

    public PullRequestContextWrapper(PullRequestReleaseVersion appVersion)
    {
        AppVersion = appVersion;
    }
}
