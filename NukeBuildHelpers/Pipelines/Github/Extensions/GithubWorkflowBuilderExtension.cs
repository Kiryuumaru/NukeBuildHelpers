using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.Pipelines.Github.Interfaces;
using System.Diagnostics.CodeAnalysis;
using NukeBuildHelpers.Pipelines.Common.Extensions;

namespace NukeBuildHelpers.Pipelines.Github.Extensions;

public static class GithubWorkflowBuilderExtension
{
    public static bool TryGetGithubWorkflowBuilder(this IWorkflowBuilder workflowBuilder, [NotNullWhen(true)] out IGithubWorkflowBuilder? builder)
    {
        return workflowBuilder.TryGetWorkflowBuilder(out builder);
    }

    public static IGithubWorkflowBuilder AddPreExecuteStep(this IGithubWorkflowBuilder workflowBuilder, Dictionary<string, object> step)
    {
        workflowBuilder.PreExecuteSteps.Add(step);
        return workflowBuilder;
    }

    public static IGithubWorkflowBuilder AddPostExecuteStep(this IGithubWorkflowBuilder workflowBuilder, Dictionary<string, object> step)
    {
        workflowBuilder.PostExecuteSteps.Add(step);
        return workflowBuilder;
    }
}
