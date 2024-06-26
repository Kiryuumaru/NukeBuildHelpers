using NukeBuildHelpers.Pipelines.Common.Interfaces;

namespace NukeBuildHelpers.Pipelines.Azure.Interfaces;

/// <summary>
/// Represents an interface for Azure-specific workflow builders.
/// </summary>
public interface IAzureWorkflowBuilder : IWorkflowBuilder
{
    internal List<Dictionary<string, object>> PreExecuteSteps { get; }

    internal List<Dictionary<string, object>> PostExecuteSteps { get; }
}
