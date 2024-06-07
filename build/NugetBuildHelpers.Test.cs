using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using NukeBuildHelpers;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Models;
using System;

namespace _build;

class NugetBuildHelpers_Test : TestEntry<Build>
{
    public override string[] TargetAppIds => ["nuget_build_helpers"];

    public override RunnerOS RunnerOS => RunnerOS.WindowsLatest;

    public override void Run(AppTestRunContext appTestRunContext)
    {
        DotNetTasks.DotNetClean(_ => _
            .SetProject(RootDirectory / "NukeBuildHelpers.UnitTest" / "NukeBuildHelpers.UnitTest.csproj"));
        DotNetTasks.DotNetTest(_ => _
            .SetProjectFile(RootDirectory / "NukeBuildHelpers.UnitTest" / "NukeBuildHelpers.UnitTest.csproj"));
    }
}
