using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using NukeBuildHelpers;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Models.RunContext;
using System;

namespace _build;

class NugetBuildHelpersTest : AppTestEntry<Build>
{
    public override RunsOnType RunsOn => RunsOnType.WindowsLatest;

    public override Type[] AppEntryTargets => [typeof(NugetBuildHelpers)];

    public override AbsolutePath[] CachePaths => [RootDirectory / "samp"];

    public override void Run(AppTestRunContext appTestRunContext)
    {
        AbsolutePath ascas = RootDirectory / "samp" / "test.txt";

        if (ascas.FileExists())
        {
            Console.WriteLine("OLD VALLLVALLLLLLVALLLLLL: " + ascas.ReadAllText());
        }

        string newVal = Guid.NewGuid().Encode();
        Console.WriteLine("NEW VALLLLLLVALLLLLL: " + newVal);
        ascas.Parent.CreateDirectory();
        ascas.WriteAllText(newVal);

        DotNetTasks.DotNetClean(_ => _
            .SetProject(RootDirectory / "NukeBuildHelpers.UnitTest" / "NukeBuildHelpers.UnitTest.csproj"));
        DotNetTasks.DotNetTest(_ => _
            .SetProjectFile(RootDirectory / "NukeBuildHelpers.UnitTest" / "NukeBuildHelpers.UnitTest.csproj"));
    }
}
