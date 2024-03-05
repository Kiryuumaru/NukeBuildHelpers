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

    [SecretHelper("NUGET_AUTH_TOKEN")]
    readonly string NuGetAuthToken;

    [SecretHelper("GITHUB_TOKEN")]
    readonly string GithubToken;

    public override bool RunParallel => false;

    public override void Build()
    {
        throw new Exception("testtt");
        OutputPath.DeleteDirectory();
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
            .SetVersion(NewVersion?.Version?.ToString() ?? "0.0.0")
            .SetPackageReleaseNotes(NewVersion.ReleaseNotes)
            .SetOutputDirectory(OutputPath));
    }

    public override void Publish()
    {
        Log.Information("Publish Release notes: {scs}", NewVersion.ReleaseNotes);
        DotNetTasks.DotNetNuGetPush(_ => _
            .SetSource("https://nuget.pkg.github.com/kiryuumaru/index.json")
            .SetApiKey(GithubToken)
            .SetTargetPath(OutputPath / "**"));
        DotNetTasks.DotNetNuGetPush(_ => _
            .SetSource("https://api.nuget.org/v3/index.json")
            .SetApiKey(NuGetAuthToken)
            .SetTargetPath(OutputPath / "**"));
    }
}
