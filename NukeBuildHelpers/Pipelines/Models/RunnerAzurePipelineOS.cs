using NukeBuildHelpers.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Pipelines.Models;

public class RunnerAzurePipelineOS : RunnerPipelineOS
{
    public virtual string? PoolName { get; init; }

    public virtual string? PoolVMImage { get; init; }
}
