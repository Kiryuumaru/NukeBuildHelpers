using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.Pipelines.Common.Models;

namespace NukeBuildHelpers.Pipelines.Common.Interfaces;

internal interface IPipeline
{
    BaseNukeBuildHelpers NukeBuild { get; set; }

    PipelineInfo GetPipelineInfo();

    PipelinePreSetup? GetPipelinePreSetup();

    Task PreparePreSetup(AllEntry allEntry);

    Task FinalizePreSetup(AllEntry allEntry, PipelinePreSetup? pipelinePreSetup);

    Task PreparePostSetup(AllEntry allEntry, PipelinePreSetup? pipelinePreSetup);

    Task FinalizePostSetup(AllEntry allEntry, PipelinePreSetup? pipelinePreSetup);

    Task PrepareEntryRun(AllEntry allEntry, PipelinePreSetup? pipelinePreSetup, Dictionary<string, IRunEntryDefinition> entriesToRunMap);

    Task FinalizeEntryRun(AllEntry allEntry, PipelinePreSetup? pipelinePreSetup, Dictionary<string, IRunEntryDefinition> entriesToRunMap);

    Task BuildWorkflow(BaseNukeBuildHelpers baseNukeBuildHelpers, AllEntry allEntry);
}
