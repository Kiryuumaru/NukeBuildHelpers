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
using NukeBuildHelpers.Attributes;

[NukeBuildCommonHelpers]
partial class Build : NukeBuild
{
    public static int Main () => Execute<Build>(x => x.Pack);

    [Solution(GenerateProjects = true)]
    readonly Solution Solution;

    readonly AbsolutePath OutputPath = RootDirectory / "output";

    Target Prepare => _ => _
        .Executes(() =>
        {
            DotNetTasks.DotNetClean(_ => _
                .SetProject(Solution.NukeBuildHelpers));
            OutputPath.DeleteDirectory();
            int asc = 1;
        });

    Target Compile => _ => _
        .DependsOn(Prepare)
        .Executes(() =>
        {
            DotNetTasks.DotNetBuild(_ => _
                .SetProjectFile(Solution.NukeBuildHelpers)
                .SetConfiguration("Release"));
        });

    Target Pack => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTasks.DotNetPack(_ => _
                .SetProject(Solution.NukeBuildHelpers)
                .SetConfiguration("Release")
                .SetNoRestore(true)
                .SetNoBuild(true)
                .SetIncludeSymbols(true)
                .SetSymbolPackageFormat("snupkg")
                .SetVersion("1.2.3-prerelease.1")
                .SetPackageReleaseNotes("Helloooow")
                .SetOutputDirectory(OutputPath / "build"));
        });

    Target Publish => _ => _
        .DependsOn(Pack)
        .Executes(() =>
        {

        });
}
