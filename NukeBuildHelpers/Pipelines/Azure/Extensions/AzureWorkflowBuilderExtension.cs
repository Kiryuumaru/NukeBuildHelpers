using NukeBuildHelpers.Pipelines.Common.Interfaces;
using System.Diagnostics.CodeAnalysis;
using NukeBuildHelpers.Pipelines.Common.Extensions;
using NukeBuildHelpers.Pipelines.Azure.Interfaces;

namespace NukeBuildHelpers.Pipelines.Azure.Extensions;

/// <summary>
/// Extension methods for <see cref="IAzureWorkflowBuilder"/>.
/// </summary>
public static class AzureWorkflowBuilderExtension
{
    /// <summary>
    /// Tries to retrieve an <see cref="IAzureWorkflowBuilder"/> from an <see cref="IWorkflowBuilder"/>.
    /// </summary>
    /// <param name="workflowBuilder">The workflow builder to retrieve.</param>
    /// <param name="builder">The retrieved Azure workflow builder, if successful.</param>
    /// <returns><c>true</c> if an Azure workflow builder is retrieved successfully; otherwise, <c>false</c>.</returns>
    public static bool TryGetAzureWorkflowBuilder(this IWorkflowBuilder workflowBuilder, [NotNullWhen(true)] out IAzureWorkflowBuilder? builder)
    {
        return workflowBuilder.TryGetWorkflowBuilder(out builder);
    }

    /// <summary>
    /// Adds a pre-execution step to the Azure workflow builder.
    /// </summary>
    /// <param name="workflowBuilder">The Azure workflow builder to add the step to.</param>
    /// <param name="step">The pre-execution step to add.</param>
    /// <returns>The Azure workflow builder with the added pre-execution step.</returns>
    public static IAzureWorkflowBuilder AddPreExecuteStep(this IAzureWorkflowBuilder workflowBuilder, Dictionary<string, object> step)
    {
        workflowBuilder.PreExecuteSteps.Add(step);
        return workflowBuilder;
    }

    /// <summary>
    /// Adds a post-execution step to the Azure workflow builder.
    /// </summary>
    /// <param name="workflowBuilder">The Azure workflow builder to add the step to.</param>
    /// <param name="step">The post-execution step to add.</param>
    /// <returns>The Azure workflow builder with the added post-execution step.</returns>
    public static IAzureWorkflowBuilder AddPostExecuteStep(this IAzureWorkflowBuilder workflowBuilder, Dictionary<string, object> step)
    {
        workflowBuilder.PostExecuteSteps.Add(step);
        return workflowBuilder;
    }
}
