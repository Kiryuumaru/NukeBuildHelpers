using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.NuGet;
using NukeBuildHelpers;
using NukeBuildHelpers.Attributes;
using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Models.RunContext;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace _build;

public class NugetBuildHelpers : AppEntry<Build>
{
    public override RunsOnType BuildRunsOn => RunsOnType.Ubuntu2204;

    public override RunsOnType PublishRunsOn => RunsOnType.Ubuntu2204;

    public override RunType RunBuildOn =>  RunType.All;

    public override RunType RunPublishOn =>  RunType.All;

    [SecretHelper("NUGET_AUTH_TOKEN")]
    readonly string? NuGetAuthToken;

    [SecretHelper("GITHUB_TOKEN")]
    readonly string? GithubToken;

    public override bool RunParallel => false;

    public override void Build(AppRunContext appRunContext)
    {
        AppVersion? appVersion = null;
        if (appRunContext is AppPipelineRunContext appPipelineRunContext)
        {
            appVersion = appPipelineRunContext.AppVersion;
        }
        OutputDirectory.DeleteDirectory();
        DotNetTasks.DotNetClean(_ => _
            .SetProject(NukeBuild.Solution.NukeBuildHelpers));
        DotNetTasks.DotNetBuild(_ => _
            .SetProjectFile(NukeBuild.Solution.NukeBuildHelpers)
            .SetConfiguration("Release"));
        DotNetTasks.DotNetPack(_ => _
            .SetProject(NukeBuild.Solution.NukeBuildHelpers)
            .SetConfiguration("Release")
            .SetNoRestore(true)
            .SetNoBuild(true)
            .SetIncludeSymbols(true)
            .SetSymbolPackageFormat("snupkg")
            .SetVersion(appVersion?.ToString() ?? "0.0.0")
            .SetPackageReleaseNotes(appVersion?.ReleaseNotes)
            .SetOutputDirectory(OutputDirectory));
    }

    public override void Publish(AppRunContext appRunContext)
    {
        if (appRunContext.RunType == RunType.Bump)
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
