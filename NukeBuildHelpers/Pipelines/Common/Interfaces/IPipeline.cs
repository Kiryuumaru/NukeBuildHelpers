using NukeBuildHelpers.Common.Models;
using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.Pipelines.Common.Models;

namespace NukeBuildHelpers.Pipelines.Common.Interfaces;

internal interface IPipeline
{
    BaseNukeBuildHelpers NukeBuild { get; set; }

    PipelineInfo GetPipelineInfo();

    void PreSetup(AllEntry allEntry, PipelinePreSetup pipelinePreSetup);

    void EntrySetup(AllEntry allEntry, PipelinePreSetup pipelinePreSetup);

    void BuildWorkflow();
}
