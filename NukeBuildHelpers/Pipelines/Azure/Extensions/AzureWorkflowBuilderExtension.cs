using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NukeBuildHelpers.Pipelines.Common.Extensions;
using NukeBuildHelpers.Pipelines.Azure.Interfaces;

namespace NukeBuildHelpers.Pipelines.Azure.Extensions;

public static class AzureWorkflowBuilderExtension
{
    public static bool TryGetAzureWorkflowBuilder(this IWorkflowBuilder workflowBuilder, [NotNullWhen(true)] out IAzureWorkflowBuilder? builder)
    {
        return workflowBuilder.TryGetWorkflowBuilder<IAzureWorkflowBuilder>(out builder);
    }

    public static IAzureWorkflowBuilder AddPreExecuteStep(this IAzureWorkflowBuilder workflowBuilder, Dictionary<string, object> step)
    {
        workflowBuilder.PreExecuteSteps.Add(step);
        return workflowBuilder;
    }

    public static IAzureWorkflowBuilder AddPostExecuteStep(this IAzureWorkflowBuilder workflowBuilder, Dictionary<string, object> step)
    {
        workflowBuilder.PostExecuteSteps.Add(step);
        return workflowBuilder;
    }
}
