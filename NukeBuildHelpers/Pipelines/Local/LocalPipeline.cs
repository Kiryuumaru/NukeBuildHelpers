using Nuke.Common.IO;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.Pipelines.Common.Models;
using System.Text.Json;

namespace NukeBuildHelpers.Pipelines.Local;

internal class LocalPipeline(BaseNukeBuildHelpers nukeBuild) : IPipeline
{
    public const string Id = "local";

    public BaseNukeBuildHelpers NukeBuild { get; set; } = nukeBuild;

    public Task<PipelineInfo> GetPipelineInfo()
    {
        return Task.FromResult(new PipelineInfo()
        {
            Branch = NukeBuild.Repository.Branch,
            TriggerType = TriggerType.Local,
            PullRequestNumber = null,
        });
    }

    public async Task<PipelinePreSetup> GetPipelinePreSetup()
    {
        await NukeBuild.RunPipelinePreSetup();
        var pipelinePreSetup = JsonSerializer.Deserialize<PipelinePreSetup?>(
        (Nuke.Common.NukeBuild.TemporaryDirectory / "NUKE_PRE_SETUP.json")
            .ReadAllText(), JsonExtension.SnakeCaseNamingOption) ?? throw new Exception("NUKE_PRE_SETUP is empty");
        return pipelinePreSetup;
    }

    public Task PreparePreSetup(AllEntry allEntry)
    {
        return Task.CompletedTask;
    }

    public Task FinalizePreSetup(AllEntry allEntry, PipelinePreSetup pipelinePreSetup)
    {
        (Nuke.Common.NukeBuild.TemporaryDirectory / "NUKE_PRE_SETUP.json")
            .WriteAllText(JsonSerializer.Serialize(pipelinePreSetup, JsonExtension.SnakeCaseNamingOption));
        return Task.CompletedTask;
    }

    public Task PreparePostSetup(AllEntry allEntry, PipelinePreSetup pipelinePreSetup)
    {
        return Task.CompletedTask;
    }

    public Task FinalizePostSetup(AllEntry allEntry, PipelinePreSetup pipelinePreSetup)
    {
        return Task.CompletedTask;
    }

    public Task PrepareEntryRun(AllEntry allEntry, PipelinePreSetup pipelinePreSetup, Dictionary<string, IRunEntryDefinition> entriesToRunMap)
    {
        return Task.CompletedTask;
    }

    public Task FinalizeEntryRun(AllEntry allEntry, PipelinePreSetup pipelinePreSetup, Dictionary<string, IRunEntryDefinition> entriesToRunMap)
    {
        return Task.CompletedTask;
    }

    public Task BuildWorkflow(BaseNukeBuildHelpers baseNukeBuildHelpers, AllEntry allEntry)
    {
        throw new NotSupportedException();
    }
}
