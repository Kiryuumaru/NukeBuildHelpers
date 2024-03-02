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

public class NugetBuildHelpersTest : AppTestEntry<Build>
{
    public override RunsOnType RunsOn => RunsOnType.WindowsLatest;

    public override bool RunParallel => false;

    public override Type[] AppEntryTargets => [typeof(NugetBuildHelpers)];

    public override void Prepare()
    {
        Console.WriteLine(Name + " Prepare");
    }

    public override void Run()
    {
        DotNetTasks.DotNetTest(_ => _
            .SetProjectFile(NukeBuild.Solution.NukeBuildHelpers_UnitTest));
    }
}
