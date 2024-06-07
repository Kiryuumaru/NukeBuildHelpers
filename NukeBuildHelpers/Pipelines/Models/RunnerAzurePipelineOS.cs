using NukeBuildHelpers.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Pipelines.Models;

public class RunnerAzurePipelineOS : RunnerPipelineOS
{
    public string? PoolName { get; init; }

    public string? PoolVMImage { get; init; }
}
