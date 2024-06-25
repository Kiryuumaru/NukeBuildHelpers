using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.Pipelines.Common.Models;

namespace NukeBuildHelpers.Pipelines.Github;

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

    public PipelinePreSetup GetPipelinePreSetup()
    {
        throw new NotImplementedException();
    }

    public Task PreparePreSetup(AllEntry allEntry)
    {
        throw new NotImplementedException();
    }

    public Task FinalizePreSetup(AllEntry allEntry, PipelinePreSetup pipelinePreSetup)
    {
        throw new NotImplementedException();
    }

    public Task PreparePostSetup(AllEntry allEntry, PipelinePreSetup pipelinePreSetup)
    {
        throw new NotImplementedException();
    }

    public Task FinalizePostSetup(AllEntry allEntry, PipelinePreSetup pipelinePreSetup)
    {
        throw new NotImplementedException();
    }

    public Task PrepareEntryRun(AllEntry allEntry, PipelinePreSetup pipelinePreSetup)
    {
        throw new NotImplementedException();
    }

    public Task FinalizeEntryRun(AllEntry allEntry, PipelinePreSetup pipelinePreSetup)
    {
        throw new NotImplementedException();
    }

    public Task BuildWorkflow(BaseNukeBuildHelpers baseNukeBuildHelpers, AllEntry allEntry)
    {
        throw new NotSupportedException();
    }
}
