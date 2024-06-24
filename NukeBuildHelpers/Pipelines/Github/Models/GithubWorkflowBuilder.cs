using NukeBuildHelpers.Pipelines.Github.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Pipelines.Github.Models;

internal class GithubWorkflowBuilder : IGithubWorkflowBuilder
{
    List<Dictionary<string, object>> IGithubWorkflowBuilder.PreExecuteSteps { get; } = [];

    List<Dictionary<string, object>> IGithubWorkflowBuilder.PostExecuteSteps { get; } = [];
}
