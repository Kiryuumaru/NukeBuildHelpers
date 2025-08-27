using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.Pipelines.Common.Enums;
using NukeBuildHelpers.RunContext.Interfaces;

namespace NukeBuildHelpers.RunContext.Models;

internal class RunContext : IRunContext
{
    public required RunType RunType { get; init; }
    public PipelineType? PipelineType { get; init; }
    public AppVersion? AppVersion { get; init; }
    public BumpReleaseVersion? BumpReleaseVersion { get; init; }
    public PullRequestReleaseVersion? PullRequestReleaseVersion { get; init; }
}
