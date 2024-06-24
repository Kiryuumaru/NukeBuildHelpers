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

internal class RunnerOSUbuntu2204 : RunnerOS
{
    public override string Name { get; } = "ubuntu-22.04";

    public override object GetPipelineOS(PipelineType pipelineType)
    {
        return pipelineType switch
        {
            PipelineType.Azure => new RunnerAzurePipelineOS() { PoolName = "Azure Pipelines", PoolVMImage = "ubuntu-22.04" },
            PipelineType.Github => new RunnerGithubPipelineOS() { RunsOn = "ubuntu-22.04" },
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
