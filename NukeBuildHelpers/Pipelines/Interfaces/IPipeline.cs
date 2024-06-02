using NukeBuildHelpers.Models;

namespace NukeBuildHelpers.Pipelines.Interfaces;

internal interface IPipeline
{
    BaseNukeBuildHelpers NukeBuild { get; set; }

    PipelineInfo GetPipelineInfo();

    void Prepare(PreSetupOutput preSetupOutput, AppConfig appConfig, Dictionary<string, AppRunEntry> toEntry);

    void BuildWorkflow();
}
