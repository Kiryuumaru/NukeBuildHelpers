using NukeBuildHelpers.Pipelines.Common.Enums;

namespace NukeBuildHelpers.RunContext.Interfaces;

public interface IPipelineContext : IRunContext
{
    PipelineType PipelineType { get; }
}
