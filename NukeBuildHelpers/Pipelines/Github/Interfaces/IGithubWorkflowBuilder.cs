using NukeBuildHelpers.Pipelines.Common.Interfaces;

namespace NukeBuildHelpers.Pipelines.Github.Interfaces;

/// <summary>
/// Defines methods and properties for building GitHub workflows.
/// </summary>
public interface IGithubWorkflowBuilder : IWorkflowBuilder
{
    internal List<Dictionary<string, object>> PreExecuteSteps { get; }

    internal List<Dictionary<string, object>> PostExecuteSteps { get; }
}
