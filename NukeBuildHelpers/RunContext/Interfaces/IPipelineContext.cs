using NukeBuildHelpers.Pipelines.Common.Enums;

namespace NukeBuildHelpers.RunContext.Interfaces;

/// <summary>
/// Represents a context that includes information specific to pipelines.
/// </summary>
public interface IPipelineContext : IRunContext
{
    /// <summary>
    /// Gets the pipeline type associated with the context.
    /// </summary>
    PipelineType PipelineType { get; }
}
