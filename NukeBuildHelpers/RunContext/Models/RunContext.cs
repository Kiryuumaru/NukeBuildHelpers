using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.Pipelines.Common.Enums;
using NukeBuildHelpers.RunContext.Interfaces;

namespace NukeBuildHelpers.RunContext.Models;

/// <summary>
/// Unified implementation of run context containing all execution information.
/// </summary>
internal class RunContext : IRunContext
{
    public required RunType RunType { get; init; }
    public required PipelineType PipelineType { get; init; }
    public required AppVersion AppVersion { get; init; }
    public BumpReleaseVersion? BumpVersion { get; init; }
    public PullRequestReleaseVersion? PullRequestVersion { get; init; }
}
