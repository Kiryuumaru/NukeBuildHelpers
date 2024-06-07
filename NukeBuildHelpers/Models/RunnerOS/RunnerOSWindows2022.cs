using NukeBuildHelpers.Pipelines.Enums;
using NukeBuildHelpers.Pipelines.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Models;

public class RunnerOSWindows2022 : RunnerOS
{
    public override RunnerPipelineOS GetPipelineOS(PipelineType pipelineType)
    {
        return pipelineType switch
        {
            PipelineType.Azure => new RunnerAzurePipelineOS() { PoolName = "Azure Pipelines", PoolVMImage = "windows-2022" },
            PipelineType.Github => new RunnerGithubPipelineOS() { RunsOn = "windows-2022" },
            _ => throw new NotImplementedException()
        };
    }

    public override string GetRunScript(PipelineType pipelineType)
    {
        return pipelineType switch
        {
            PipelineType.Azure => "./build.cmd",
            PipelineType.Github => "./build.cmd",
            _ => throw new NotImplementedException()
        };
    }
}
