using Nuke.Common.Tools.DotNet;
using NukeBuildHelpers;
using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Models;
using System;

namespace _build;

class NugetBuildHelpersTest2 : AppTestEntry<Build>
{
    public override RunnerOS RunnerOS => RunnerOS.Windows2022;

    public override RunTestType RunTestOn => RunTestType.Local;

    public override Type[] AppEntryTargets => [typeof(NugetBuildHelpers2)];

    public override void Run(AppTestRunContext appTestRunContext)
    {
        DotNetTasks.DotNetClean(_ => _
            .SetProject(RootDirectory / "NukeBuildHelpers.UnitTest" / "NukeBuildHelpers.UnitTest.csproj"));
        DotNetTasks.DotNetTest(_ => _
            .SetProjectFile(RootDirectory / "NukeBuildHelpers.UnitTest" / "NukeBuildHelpers.UnitTest.csproj"));
    }
}
