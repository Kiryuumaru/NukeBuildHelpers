using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common;
using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.Pipelines.Azure;
using NukeBuildHelpers.Pipelines.Common.Enums;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.Pipelines.Common.Models;
using NukeBuildHelpers.Pipelines.Github;
using NukeBuildHelpers.Pipelines.Local;

namespace NukeBuildHelpers.Pipelines.Common;

internal static class PipelineHelpers
{
    internal static Task BuildWorkflow<T>(BaseNukeBuildHelpers baseNukeBuildHelpers, AllEntry allEntry)
        where T : IPipeline
    {
        return (Activator.CreateInstance(typeof(T), baseNukeBuildHelpers) as IPipeline)!.BuildWorkflow(baseNukeBuildHelpers, allEntry);
    }

    internal static PipelineRun SetupPipeline(BaseNukeBuildHelpers baseNukeBuildHelpers)
    {
        IPipeline pipeline;
        PipelineType pipelineType;

        if (NukeBuild.Host is AzurePipelines)
        {
            pipeline = new AzurePipeline(baseNukeBuildHelpers);
            pipelineType = PipelineType.Azure;
        }
        else if (NukeBuild.Host is GitHubActions)
        {
            pipeline = new GithubPipeline(baseNukeBuildHelpers);
            pipelineType = PipelineType.Github;
        }
        else
        {
            pipeline = new LocalPipeline(baseNukeBuildHelpers);
            pipelineType = PipelineType.Local;
        }

        return new()
        {
            Pipeline = pipeline,
            PipelineInfo = pipeline.GetPipelineInfo(),
            PipelineType = pipelineType,
        };
    }
}
