using NukeBuildHelpers.Pipelines.Azure.Models;
using NukeBuildHelpers.Pipelines.Common.Enums;
using NukeBuildHelpers.Pipelines.Github.Models;
using NukeBuildHelpers.Runner.Abstraction;

namespace NukeBuildHelpers.Runner.Models;

internal class RunnerOSUbuntu2004 : RunnerOS
{
    public override string Name { get; } = "ubuntu-20.04";

    public override object GetPipelineOS(PipelineType pipelineType)
    {
        return pipelineType switch
        {
            PipelineType.Azure => new RunnerAzurePipelineOS() { PoolName = "Azure Pipelines", PoolVMImage = "ubuntu-20.04" },
            PipelineType.Github => new RunnerGithubPipelineOS() { RunsOn = "ubuntu-20.04" },
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
