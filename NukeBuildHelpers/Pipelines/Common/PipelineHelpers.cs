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
using Nuke.Common.Utilities;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Entry.Helpers;
using Nuke.Common.Tooling;
using NukeBuildHelpers.Common.Models;

namespace NukeBuildHelpers.Pipelines.Common;

internal static class PipelineHelpers
{
    internal static async Task BuildWorkflow<T>(BaseNukeBuildHelpers baseNukeBuildHelpers, AllEntry allEntry)
        where T : IPipeline
    {
        if (await allEntry.WorkflowConfigEntryDefinition.GetUseJsonFileVersioning())
        {
            await EntryHelpers.GenerateAllVersionsFile(baseNukeBuildHelpers, allEntry);
        }

        await (Activator.CreateInstance(typeof(T), baseNukeBuildHelpers) as IPipeline)!.BuildWorkflow(baseNukeBuildHelpers, allEntry);
    }

    internal static async Task<PipelineRun> SetupPipeline(BaseNukeBuildHelpers baseNukeBuildHelpers)
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
            PipelineInfo = await pipeline.GetPipelineInfo(),
            PipelineType = pipelineType,
        };
    }
}
