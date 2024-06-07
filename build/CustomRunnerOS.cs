using NukeBuildHelpers.Models;
using NukeBuildHelpers.Pipelines.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Pipelines.Models;

public class CustomRunnerOS : RunnerOSUbuntu2204
{
    public override RunnerPipelineOS GetPipelineOS(PipelineType pipelineType)
    {
        if (pipelineType == PipelineType.Github)
        {
            return base.GetPipelineOS(pipelineType);
        }

        return new RunnerAzurePipelineOS()
        {
            PoolName = "Pipeline na Custom",
            PoolVMImage = null
        };
    }
}
