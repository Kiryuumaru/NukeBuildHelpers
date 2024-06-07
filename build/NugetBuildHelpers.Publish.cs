using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.NuGet;
using NukeBuildHelpers;
using NukeBuildHelpers.Attributes;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Models;
using System;

namespace _build;

class NugetBuildHelpers_Publish : PublishEntry<Build>
{
    public override string Id => "nuget_build_helpers";

    public override RunnerOS RunnerOS => RunnerOS.Ubuntu2204;

    public override RunType RunOn => RunType.All;

    [SecretVariable("NUGET_AUTH_TOKEN")]
    readonly string? NuGetAuthToken;

    [SecretVariable("GITHUB_TOKEN")]
    readonly string? GithubToken;

    public override void Run(AppRunContext runContext)
    {
        if (runContext.RunType == RunType.Bump && PipelineType == NukeBuildHelpers.Pipelines.Enums.PipelineType.Github)
        {
            DotNetTasks.DotNetNuGetPush(_ => _
                .SetSource("https://nuget.pkg.github.com/kiryuumaru/index.json")
                .SetApiKey(GithubToken)
                .SetTargetPath(OutputDirectory / "**"));
            DotNetTasks.DotNetNuGetPush(_ => _
                .SetSource("https://api.nuget.org/v3/index.json")
                .SetApiKey(NuGetAuthToken)
                .SetTargetPath(OutputDirectory / "**"));
        }
    }
}
