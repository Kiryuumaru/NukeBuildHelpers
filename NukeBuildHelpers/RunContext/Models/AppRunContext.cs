using Nuke.Common.IO;
using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.Pipelines.Common.Enums;
using NukeBuildHelpers.RunContext.Interfaces;

namespace NukeBuildHelpers.RunContext.Models;

/// <summary>
/// Run context versions containing application and release version information.
/// </summary>
public class AppRunContext
{
    /// <summary>
    /// Gets the app id associated with the context.
    /// </summary>
    public required string AppId { get; init; }

    /// <summary>
    /// Gets the run type associated with the context.
    /// </summary>
    public required RunType RunType { get; init; }

    /// <summary>
    /// Gets the application version associated with the context.
    /// </summary>
    public required AppVersion AppVersion { get; init; }

    /// <summary>
    /// Gets the bump release version with release notes. Null unless this is a bump run.
    /// </summary>
    public BumpReleaseVersion? BumpVersion { get; init; }

    /// <summary>
    /// Gets the pull request release version with PR number. Null unless this is a pull request run.
    /// </summary>
    public PullRequestReleaseVersion? PullRequestVersion { get; init; }

    /// <summary>
    /// Gets the output directory path where this application's build artifacts and files are stored during pipeline execution.
    /// The path is constructed by combining the common runtime output directory with the lowercase application ID.
    /// </summary>
    public AbsolutePath OutputDirectory => BaseNukeBuildHelpers.CommonOutputDirectory / "runtime" / AppId.ToLowerInvariant();

    /// <summary>
    /// Gets whether this is a version bump run.
    /// </summary>
    public bool IsBump => BumpVersion != null;

    /// <summary>
    /// Gets whether this is a pull request run.
    /// </summary>
    public bool IsPullRequest => PullRequestVersion != null;

    /// <summary>
    /// Gets whether this is a local development run.
    /// </summary>
    public bool IsLocal => RunType == RunType.Local;

    /// <summary>
    /// Gets whether this is a commit-based run.
    /// </summary>
    public bool IsCommit => RunType == RunType.Commit;
}
