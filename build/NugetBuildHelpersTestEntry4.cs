using Nuke.Common.Tools.DotNet;
using NukeBuildHelpers;
using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Models;
using System;

namespace _build;

class NugetBuildHelpersTest4 : AppTestEntry<Build>
{
    public override RunnerOS RunnerOS => RunnerOS.Ubuntu2204;

    public override RunTestType RunTestOn => RunTestType.Target;

    public override Type[] AppEntryTargets => [typeof(NugetBuildHelpers3)];

    public override void Run(AppTestRunContext appTestRunContext)
    {
        DotNetTasks.DotNetClean(_ => _
            .SetProject(RootDirectory / "NukeBuildHelpers.UnitTest" / "NukeBuildHelpers.UnitTest.csproj"));
        DotNetTasks.DotNetTest(_ => _
            .SetProjectFile(RootDirectory / "NukeBuildHelpers.UnitTest" / "NukeBuildHelpers.UnitTest.csproj"));
    }
}
