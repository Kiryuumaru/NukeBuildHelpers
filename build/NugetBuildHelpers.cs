using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.NuGet;
using NukeBuildHelpers;
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

    public override bool RunParallel => false;

    public override void Build()
    {
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
            .SetPackageReleaseNotes("* Initial prerelease")
            .SetOutputDirectory(OutputPath));
    }

    public override void Publish()
    {
        NuGetTasks.NuGetPush(_ => _
            .SetSource("https://nuget.pkg.github.com/kiryuumaru/index.json")
            .SetApiKey(NukeBuild.GithubToken)
            .CombineWith(OutputPath.GetFiles("*.zip"), (_, v) => _.SetTargetPath(v)));
        NuGetTasks.NuGetPush(_ => _
            .SetSource("https://api.nuget.org/v3/index.json")
            .SetApiKey(NukeBuild.NuGetAuthToken)
            .CombineWith(OutputPath.GetFiles(), (_, v) => _.SetTargetPath(v)));
    }
}
