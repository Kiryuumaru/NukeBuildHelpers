using NukeBuildHelpers.Pipelines.Common.Interfaces;

namespace NukeBuildHelpers.Pipelines.Azure.Interfaces;

public interface IAzureWorkflowBuilder : IWorkflowBuilder
{
    internal List<Dictionary<string, object>> PreExecuteSteps { get; }

    internal List<Dictionary<string, object>> PostExecuteSteps { get; }
}
