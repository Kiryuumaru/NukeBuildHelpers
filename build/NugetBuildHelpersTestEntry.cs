using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using NukeBuildHelpers;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Models;
using NukeBuildHelpers.Pipelines.Models;
using System;

namespace _build;

class NugetBuildHelpersTest : AppTestEntry<Build>
{
    public override RunnerOS RunnerOS => new CustomRunnerOS();

    public override Type[] AppEntryTargets => [typeof(NugetBuildHelpers)];

    public override void Run(AppTestRunContext appTestRunContext)
    {
        DotNetTasks.DotNetClean(_ => _
            .SetProject(RootDirectory / "NukeBuildHelpers.UnitTest" / "NukeBuildHelpers.UnitTest.csproj"));
        DotNetTasks.DotNetTest(_ => _
            .SetProjectFile(RootDirectory / "NukeBuildHelpers.UnitTest" / "NukeBuildHelpers.UnitTest.csproj"));
    }
}
