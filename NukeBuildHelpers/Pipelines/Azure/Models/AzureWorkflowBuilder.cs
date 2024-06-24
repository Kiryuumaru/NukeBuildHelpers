using NukeBuildHelpers.Pipelines.Azure.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Pipelines.Azure.Models;

internal class AzureWorkflowBuilder : IAzureWorkflowBuilder
{
    List<Dictionary<string, object>> IAzureWorkflowBuilder.PreExecuteSteps { get; } = [];

    List<Dictionary<string, object>> IAzureWorkflowBuilder.PostExecuteSteps { get; } = [];
}
