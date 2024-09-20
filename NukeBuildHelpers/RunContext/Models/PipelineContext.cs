using NukeBuildHelpers.Pipelines.Common.Enums;
using NukeBuildHelpers.RunContext.Interfaces;

namespace NukeBuildHelpers.RunContext.Models;

internal abstract class PipelineContext : RunContext, IPipelineContext
{
    public required PipelineType PipelineType { get; init; }

    PipelineType IPipelineContext.PipelineType { get => PipelineType; }
}
