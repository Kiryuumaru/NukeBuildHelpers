using NukeBuildHelpers.Runner.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Pipelines.Github.Models;

/// <summary>
/// Represents the GitHub pipeline-specific operating system.
/// </summary>
public class RunnerGithubPipelineOS : RunnerPipelineOS
{
    /// <summary>
    /// Gets or sets the runner that the job will run on.
    /// </summary>
    public virtual string? RunsOn { get; init; }

    /// <summary>
    /// Gets or sets the labels of the runners that the job will run on.
    /// </summary>
    public virtual string[]? RunsOnLabels { get; init; }
}
