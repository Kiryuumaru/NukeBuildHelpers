using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.Pipelines.Common.Models;

namespace NukeBuildHelpers.Pipelines.Local;

internal class LocalPipeline(BaseNukeBuildHelpers nukeBuild) : IPipeline
{
    public const string Id = "local";

    public BaseNukeBuildHelpers NukeBuild { get; set; } = nukeBuild;

    public PipelineInfo GetPipelineInfo()
    {
        return new()
        {
            Branch = NukeBuild.Repository.Branch,
            TriggerType = TriggerType.Local,
            PullRequestNumber = 0,
        };
    }

    public PipelinePreSetup? GetPipelinePreSetup()
    {
        return null;
    }

    public Task PreparePreSetup(AllEntry allEntry)
    {
        return Task.CompletedTask;
    }

    public Task FinalizePreSetup(AllEntry allEntry, PipelinePreSetup? pipelinePreSetup)
    {
        return Task.CompletedTask;
    }

    public Task PreparePostSetup(AllEntry allEntry, PipelinePreSetup? pipelinePreSetup)
    {
        return Task.CompletedTask;
    }

    public Task FinalizePostSetup(AllEntry allEntry, PipelinePreSetup? pipelinePreSetup)
    {
        return Task.CompletedTask;
    }

    public Task PrepareEntryRun(AllEntry allEntry, PipelinePreSetup? pipelinePreSetup, Dictionary<string, IEntryDefinition> entriesToRunMap)
    {
        return Task.CompletedTask;
    }

    public Task FinalizeEntryRun(AllEntry allEntry, PipelinePreSetup? pipelinePreSetup, Dictionary<string, IEntryDefinition> entriesToRunMap)
    {
        return Task.CompletedTask;
    }

    public Task BuildWorkflow(BaseNukeBuildHelpers baseNukeBuildHelpers, AllEntry allEntry)
    {
        throw new NotSupportedException();
    }
}
