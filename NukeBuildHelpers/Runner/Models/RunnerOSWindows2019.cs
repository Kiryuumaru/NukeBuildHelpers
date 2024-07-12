using NukeBuildHelpers.Pipelines.Azure.Models;
using NukeBuildHelpers.Pipelines.Common.Enums;
using NukeBuildHelpers.Pipelines.Github.Models;
using NukeBuildHelpers.Runner.Abstraction;

namespace NukeBuildHelpers.Runner.Models;

internal class RunnerOSWindows2019 : RunnerOS
{
    public override string Name { get; } = "windows-2019";

    public override object GetPipelineOS(PipelineType pipelineType)
    {
        return pipelineType switch
        {
            PipelineType.Azure => new RunnerAzurePipelineOS() { PoolName = "Azure Pipelines", PoolVMImage = "windows-2019" },
            PipelineType.Github => new RunnerGithubPipelineOS() { RunsOn = "windows-2019" },
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
