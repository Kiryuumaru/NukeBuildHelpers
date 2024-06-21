using Microsoft.Identity.Client;
using Nuke.Common;
using Nuke.Common.IO;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Common.Attributes;
using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Common.Models;
using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.Pipelines.Common.Enums;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.Pipelines.Common.Models;
using NukeBuildHelpers.Pipelines.Github.Models;
using NukeBuildHelpers.Runner.Abstraction;
using Serilog;
using System.Linq;
using System.Reflection;
using System.Text.Json;

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

    public Task PreSetup(AllEntry allEntry, PipelinePreSetup pipelinePreSetup)
    {
        throw new NotSupportedException();
    }

    public void EntrySetup(AllEntry allEntry, PipelinePreSetup pipelinePreSetup)
    {
        throw new NotSupportedException();
    }

    public void BuildWorkflow(BaseNukeBuildHelpers baseNukeBuildHelpers, AllEntry allEntry)
    {
        throw new NotSupportedException();
    }
}
