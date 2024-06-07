using NukeBuildHelpers.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Pipelines.Models;

/// <summary>
/// Represents the Azure pipeline-specific operating system.
/// </summary>
public class RunnerAzurePipelineOS : RunnerPipelineOS
{
    /// <summary>
    /// Gets or sets the name of the pool.
    /// </summary>
    public virtual string? PoolName { get; init; }

    /// <summary>
    /// Gets or sets the virtual machine image in the pool.
    /// </summary>
    public virtual string? PoolVMImage { get; init; }
}
