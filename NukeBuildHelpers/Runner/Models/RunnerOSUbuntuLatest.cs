using NukeBuildHelpers.Pipelines.Azure.Models;
using NukeBuildHelpers.Pipelines.Common.Enums;
using NukeBuildHelpers.Pipelines.Github.Models;
using NukeBuildHelpers.Runner.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Runner.Models;

internal class RunnerOSUbuntuLatest : RunnerOS
{
    public override string Name { get; } = "ubuntu-latest";

    public override object GetPipelineOS(PipelineType pipelineType)
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
