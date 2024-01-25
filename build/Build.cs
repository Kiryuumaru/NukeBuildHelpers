using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using NukeBuildHelpers;
using Serilog;
using NukeBuildHelpers.Models;
using Nuke.Common.Tools.DotNet;
using System.Threading.Tasks;

public partial class Build : BaseNukeBuildHelpers
{
    public static int Main () => Execute<Build>(x => x.Pack);

    [Solution(GenerateProjects = true)]
    readonly Solution Solution;

    readonly AbsolutePath OutputPath = RootDirectory / "output";

    protected override Task OnPrepare()
    {
        DotNetTasks.DotNetClean(_ => _
            .SetProject(Solution.NukeBuildHelpers));
        OutputPath.DeleteDirectory();
        int asc = 1;
        return Task.CompletedTask;
    }

    protected override Task OnBuild()
    {
        DotNetTasks.DotNetBuild(_ => _
            .SetProjectFile(Solution.NukeBuildHelpers)
            .SetConfiguration("Release"));
        return Task.CompletedTask;
    }

    protected override Task OnPack()
    {
        DotNetTasks.DotNetPack(_ => _
            .SetProject(Solution.NukeBuildHelpers)
            .SetConfiguration("Release")
            .SetNoRestore(true)
            .SetNoBuild(true)
            .SetIncludeSymbols(true)
            .SetSymbolPackageFormat("snupkg")
            .SetVersion("0.1.0-prerelease.1")
            .SetPackageReleaseNotes("* Initial prerelease")
            .SetOutputDirectory(OutputPath / "build"));
        return Task.CompletedTask;
    }

    protected override Task OnPublish()
    {
        return Task.CompletedTask;
    }
}
