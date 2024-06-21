using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.Pipelines.Azure;
using NukeBuildHelpers.Pipelines.Common.Enums;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.Pipelines.Common.Models;
using NukeBuildHelpers.Pipelines.Github;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Pipelines.Common;

internal static class PipelineHelpers
{
    internal static void BuildWorkflow<T>(BaseNukeBuildHelpers baseNukeBuildHelpers)
        where T : IPipeline
    {
        (Activator.CreateInstance(typeof(T), baseNukeBuildHelpers) as IPipeline)!.BuildWorkflow();
    }

    internal static PipelineRun? SetupPipeline(BaseNukeBuildHelpers baseNukeBuildHelpers)
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
            return null;
        }

        return new()
        {
            Pipeline = pipeline,
            PipelineInfo = pipeline.GetPipelineInfo(),
            PipelineType = pipelineType,
        };
    }
}
