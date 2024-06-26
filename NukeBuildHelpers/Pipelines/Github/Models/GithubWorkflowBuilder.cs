using NukeBuildHelpers.Pipelines.Github.Interfaces;

namespace NukeBuildHelpers.Pipelines.Github.Models;

internal class GithubWorkflowBuilder : IGithubWorkflowBuilder
{
    List<Dictionary<string, object>> IGithubWorkflowBuilder.PreExecuteSteps { get; } = [];

    List<Dictionary<string, object>> IGithubWorkflowBuilder.PostExecuteSteps { get; } = [];
}
