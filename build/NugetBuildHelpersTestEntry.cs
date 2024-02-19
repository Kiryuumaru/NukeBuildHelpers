using Nuke.Common;
using Nuke.Common.IO;
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
    public override BuildsOnType BuildsOn => BuildsOnType.Ubuntu2204;

    public override Type[] AppEntryTargets => new Type[] { typeof(NugetBuildHelpers) };

    public override void Prepare(Build nukeBuild)
    {
        Console.WriteLine(Name + " Prepare");
    }

    public override void Run(Build nukeBuild)
    {
        Console.WriteLine(Name + " Build");
    }
}
