using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using NukeBuildHelpers;
using NukeBuildHelpers.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _build;

public class NugetBuildHelpers3 : AppEntry<Build>
{
    public override RunsOnType RunsOn => RunsOnType.UbuntuLatest;

    public override bool MainRelease => false;

    public override bool RunParallel => false;

    public override void Prepare(Build nukeBuild, AbsolutePath outputPath)
    {
        DotNetTasks.DotNetClean(_ => _
            .SetProject(nukeBuild.Solution.NukeBuildHelpers));
        outputPath.DeleteDirectory();
    }

    public override void Build(Build nukeBuild, AbsolutePath outputPath)
    {
        DotNetTasks.DotNetBuild(_ => _
            .SetProjectFile(nukeBuild.Solution.NukeBuildHelpers)
            .SetConfiguration("Release"));
    }

    public override void Pack(Build nukeBuild, AbsolutePath outputPath)
    {
        DotNetTasks.DotNetPack(_ => _
            .SetProject(nukeBuild.Solution.NukeBuildHelpers)
            .SetConfiguration("Release")
            .SetNoRestore(true)
            .SetNoBuild(true)
            .SetIncludeSymbols(true)
            .SetSymbolPackageFormat("snupkg")
            .SetVersion("0.1.0-prerelease.1")
            .SetPackageReleaseNotes("* Initial prerelease")
            .SetOutputDirectory(outputPath));
    }

    public override void Publish(Build nukeBuild, AbsolutePath outputPath)
    {
        Console.WriteLine(Name + " Release");
    }
}
