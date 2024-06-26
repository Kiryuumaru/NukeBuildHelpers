using NukeBuildHelpers.Pipelines.Azure.Interfaces;

namespace NukeBuildHelpers.Pipelines.Azure.Models;

internal class AzureWorkflowBuilder : IAzureWorkflowBuilder
{
    List<Dictionary<string, object>> IAzureWorkflowBuilder.PreExecuteSteps { get; } = [];

    List<Dictionary<string, object>> IAzureWorkflowBuilder.PostExecuteSteps { get; } = [];
}
