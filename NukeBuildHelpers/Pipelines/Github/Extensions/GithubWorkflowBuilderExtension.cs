using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.Pipelines.Github.Interfaces;
using System.Diagnostics.CodeAnalysis;
using NukeBuildHelpers.Pipelines.Common.Extensions;

namespace NukeBuildHelpers.Pipelines.Github.Extensions;

/// <summary>
/// Provides extension methods for GitHub workflow builders.
/// </summary>
public static class GithubWorkflowBuilderExtension
{
    /// <summary>
    /// Tries to get an instance of <see cref="IGithubWorkflowBuilder"/> from the given <see cref="IWorkflowBuilder"/>.
    /// </summary>
    /// <param name="workflowBuilder">The workflow builder instance.</param>
    /// <param name="builder">When this method returns, contains the <see cref="IGithubWorkflowBuilder"/> if the conversion succeeded, or <c>null</c> if it failed.</param>
    /// <returns><c>true</c> if the conversion succeeded; otherwise, <c>false</c>.</returns>
    public static bool TryGetGithubWorkflowBuilder(this IWorkflowBuilder workflowBuilder, [NotNullWhen(true)] out IGithubWorkflowBuilder? builder)
    {
        return workflowBuilder.TryGetWorkflowBuilder(out builder);
    }

    /// <summary>
    /// Adds a pre-execution step to the GitHub workflow builder.
    /// </summary>
    /// <param name="workflowBuilder">The GitHub workflow builder instance.</param>
    /// <param name="step">The pre-execution step to add.</param>
    /// <returns>The modified <see cref="IGithubWorkflowBuilder"/> instance.</returns>
    public static IGithubWorkflowBuilder AddPreExecuteStep(this IGithubWorkflowBuilder workflowBuilder, Dictionary<string, object> step)
    {
        workflowBuilder.PreExecuteSteps.Add(step);
        return workflowBuilder;
    }

    /// <summary>
    /// Adds a post-execution step to the GitHub workflow builder.
    /// </summary>
    /// <param name="workflowBuilder">The GitHub workflow builder instance.</param>
    /// <param name="step">The post-execution step to add.</param>
    /// <returns>The modified <see cref="IGithubWorkflowBuilder"/> instance.</returns>
    public static IGithubWorkflowBuilder AddPostExecuteStep(this IGithubWorkflowBuilder workflowBuilder, Dictionary<string, object> step)
    {
        workflowBuilder.PostExecuteSteps.Add(step);
        return workflowBuilder;
    }
}
