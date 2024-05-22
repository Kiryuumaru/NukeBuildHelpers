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

public class NugetBuildHelpers2 : AppEntry<Build>
{
    public override RunsOnType BuildRunsOn => RunsOnType.Ubuntu2204;

    public override RunsOnType PublishRunsOn => RunsOnType.WindowsLatest;

    public override RunType RunBuildOn => RunType.Bump;

    public override RunType RunPublishOn => RunType.Bump;

    public override bool MainRelease => false;

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
            .SetVersion(appRunContext.AppVersion?.Version.ToString() ?? "0.0.0")
            .SetPackageReleaseNotes("* Initial prerelease")
            .SetOutputDirectory(OutputDirectory));
    }

    public override void Publish(AppRunContext appRunContext)
    {
        if (appRunContext.RunType == RunType.Bump)
        {
            foreach (var ss in OutputDirectory.GetFiles())
            {
                Log.Information("Publish: {name}", ss.Name);
            }
        }
    }
}
