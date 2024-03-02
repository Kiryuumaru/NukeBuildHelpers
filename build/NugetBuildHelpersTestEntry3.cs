using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using NukeBuildHelpers;
using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _build;

public class NugetBuildHelpersTest3 : AppTestEntry<Build>
{
    public override RunsOnType RunsOn => RunsOnType.Ubuntu2204;

    public override bool RunParallel => false;

    public override Type[] AppEntryTargets => [typeof(NugetBuildHelpers), typeof(NugetBuildHelpers2)];

    public override void Prepare(Build nukeBuild)
    {
        Console.WriteLine(Name + " Prepare");
    }

    public override void Run(Build nukeBuild)
    {
        DotNetTasks.DotNetTest(_ => _
            .SetProjectFile(nukeBuild.Solution.NukeBuildHelpers_UnitTest));
    }
}
