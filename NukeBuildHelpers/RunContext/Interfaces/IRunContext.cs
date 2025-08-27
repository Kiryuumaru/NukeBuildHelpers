using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.Pipelines.Common.Enums;
using NukeBuildHelpers.RunContext.Models;

namespace NukeBuildHelpers.RunContext.Interfaces;

/// <summary>
/// Represents a context that includes run type information.
/// </summary>
public interface IRunContext
{
    /// <summary>
    /// Gets the pipeline type associated with the context.
    /// </summary>
    PipelineType PipelineType { get; }

    /// <summary>
    /// Gets the application versions associated with the context.
    /// </summary>
    IReadOnlyDictionary<string, RunContextVersion> Versions { get; }
}
