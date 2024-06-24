using NukeBuildHelpers.Pipelines.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Pipelines.Github.Interfaces;

public interface IGithubWorkflowBuilder : IWorkflowBuilder
{
    internal List<Dictionary<string, object>> PreExecuteSteps { get; }

    internal List<Dictionary<string, object>> PostExecuteSteps { get; }
}
