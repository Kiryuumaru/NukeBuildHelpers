using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using NukeBuildHelpers;
using NukeBuildHelpers.Enums;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _build;

public class NugetBuildHelpers3 : AppEntry<Build>
{
    public override RunsOnType BuildRunsOn => RunsOnType.Windows2022;

    public override RunsOnType PublishRunsOn => RunsOnType.UbuntuLatest;

    public override bool MainRelease => false;

    public override bool RunParallel => false;

    public override void Build()
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
            .SetVersion(NewVersion?.Version.ToString() ?? "0.0.0")
            .SetPackageReleaseNotes("* Initial prerelease")
            .SetOutputDirectory(OutputDirectory));
    }

    public override void Publish()
    {
        foreach (var ss in OutputDirectory.GetFiles())
        {
            Log.Information("Publish: {name}", ss.Name);
        }
    }
}
