using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using NukeBuildHelpers;
using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Models;
using NukeBuildHelpers.Models.RunContext;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _build;

public class NugetBuildHelpersTest3 : AppTestEntry<Build>
{
    public override RunsOnType RunsOn => RunsOnType.Ubuntu2204;

    public override RunTestType RunTestOn => RunTestType.All;

    public override bool RunParallel => false;

    public override Type[] AppEntryTargets => [typeof(NugetBuildHelpers2)];

    public override void Run(AppTestRunContext appTestRunContext)
    {
        DotNetTasks.DotNetClean(_ => _
            .SetProject(NukeBuild.Solution.NukeBuildHelpers_UnitTest));
        DotNetTasks.DotNetTest(_ => _
            .SetProjectFile(NukeBuild.Solution.NukeBuildHelpers_UnitTest));
    }
}
