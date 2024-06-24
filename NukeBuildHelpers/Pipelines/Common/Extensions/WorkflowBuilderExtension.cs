using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Pipelines.Common.Extensions;

public static class WorkflowBuilderExtension
{
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
