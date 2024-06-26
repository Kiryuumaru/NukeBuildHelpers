using NukeBuildHelpers.Common.Enums;

namespace NukeBuildHelpers.Pipelines.Common.Models;

internal class PipelineInfo
{
    public required string Branch { get; init; }

    public required TriggerType TriggerType { get; init; }

    public required long PullRequestNumber { get; init; }
}
