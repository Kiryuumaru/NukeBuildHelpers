using NukeBuildHelpers.Pipelines.Common.Interfaces;
using System.Diagnostics.CodeAnalysis;

namespace NukeBuildHelpers.Pipelines.Common.Extensions;

/// <summary>
/// Provides extension methods for workflow builders.
/// </summary>
public static class WorkflowBuilderExtension
{
    /// <summary>
    /// Tries to get an instance of <typeparamref name="TWorkflowBuilder"/> from the given <see cref="IWorkflowBuilder"/>.
    /// </summary>
    /// <typeparam name="TWorkflowBuilder">The type of workflow builder to retrieve.</typeparam>
    /// <param name="builder">The workflow builder instance.</param>
    /// <param name="workflowBuilder">When this method returns, contains the <typeparamref name="TWorkflowBuilder"/> if the conversion succeeded, or <c>null</c> if it failed.</param>
    /// <returns><c>true</c> if the conversion succeeded; otherwise, <c>false</c>.</returns>
    public static bool TryGetWorkflowBuilder<TWorkflowBuilder>(this IWorkflowBuilder builder, [NotNullWhen(true)] out TWorkflowBuilder? workflowBuilder)
        where TWorkflowBuilder : IWorkflowBuilder
    {
        if (builder is TWorkflowBuilder wb)
        {
            workflowBuilder = wb;
            return true;
        }
        workflowBuilder = default;
        return false;
    }
}
