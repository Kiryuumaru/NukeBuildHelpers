using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Pipelines.Enums;
using NukeBuildHelpers.Pipelines.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Models;

public class RunnerOSUbuntuLatest : RunnerOS
{
    public override RunnerPipelineOS GetPipelineOS(PipelineType pipelineType)
    {
        return pipelineType switch
        {
            PipelineType.Azure => new RunnerAzurePipelineOS() { PoolName = "Azure Pipelines", PoolVMImage = "ubuntu-latest" },
            PipelineType.Github => new RunnerGithubPipelineOS() { RunsOn = "ubuntu-latest" },
            _ => throw new NotImplementedException()
        };
    }

    public override string GetRunScript(PipelineType pipelineType)
    {
        return pipelineType switch
        {
            PipelineType.Azure => "chmod +x ./build.sh && ./build.sh",
            PipelineType.Github => "chmod +x ./build.sh && ./build.sh",
            _ => throw new NotImplementedException()
        };
    }
}
