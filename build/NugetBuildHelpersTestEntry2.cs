﻿using Nuke.Common;
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

public class NugetBuildHelpersTest2 : AppTestEntry<Build>
{
    public override RunsOnType RunsOn => RunsOnType.Windows2022;

    public override Type[] AppEntryTargets => new Type[] { typeof(NugetBuildHelpers2) };

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