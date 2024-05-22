using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.NuGet;
using NukeBuildHelpers;
using NukeBuildHelpers.Attributes;
using NukeBuildHelpers.Enums;
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

    public override RunType RunPublishOn =>  RunType.Bump;

    [SecretHelper("NUGET_AUTH_TOKEN")]
    readonly string NuGetAuthToken;

    [SecretHelper("GITHUB_TOKEN")]
    readonly string GithubToken;

    public override bool RunParallel => false;

    public override void Build(AppRunContext appRunContext)
    {
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
            .SetVersion(appRunContext.AppVersion?.Version?.ToString() ?? "0.0.0")
            .SetPackageReleaseNotes(appRunContext.AppVersion?.ReleaseNotes)
            .SetOutputDirectory(OutputDirectory));
    }

    public override void Publish(AppRunContext appRunContext)
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
