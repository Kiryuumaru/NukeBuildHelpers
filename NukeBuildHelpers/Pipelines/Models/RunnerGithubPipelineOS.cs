using NukeBuildHelpers.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Pipelines.Models;

public class RunnerGithubPipelineOS : RunnerPipelineOS
{
    public virtual string? RunsOn { get; init; }

    public virtual string[]? RunsOnLabels { get; init; }
}
