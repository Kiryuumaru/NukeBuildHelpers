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
    public required PipelineType PipelineType { get; init; }
    public required IReadOnlyDictionary<string, RunContextVersion> Versions { get; init; }
}
