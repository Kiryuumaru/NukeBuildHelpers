using NukeBuildHelpers.Pipelines.Common.Enums;
using NukeBuildHelpers.Pipelines.Common.Interfaces;

namespace NukeBuildHelpers.Pipelines.Common.Models;

internal class PipelineRun
{
    public required IPipeline Pipeline { get; init; }

    public required PipelineInfo PipelineInfo { get; init; }

    public required PipelineType PipelineType { get; init; }
}
