using NukeBuildHelpers.Pipelines.Common.Enums;

namespace NukeBuildHelpers.RunContext.Interfaces;

internal abstract class PipelineContext : RunContext, IPipelineContext
{
    public required PipelineType PipelineType { get; init; }

    PipelineType IPipelineContext.PipelineType { get => PipelineType; }
}
