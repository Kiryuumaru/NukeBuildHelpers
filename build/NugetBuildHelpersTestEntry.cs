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

    public override AbsolutePath[] CachePaths => [RootDirectory / "samp1", RootDirectory / "samp" / "test.txt"];

    public override void Run(AppTestRunContext appTestRunContext)
    {
        AbsolutePath ascas1 = RootDirectory / "samp1" / "test.txt";

        if (ascas1.FileExists())
        {
            Console.WriteLine("1OLD VALLLVALLLLLLVALLLdsdLLLs1: " + ascas1.ReadAllText());
        }

        string newVal1 = Guid.NewGuid().Encode();
        Console.WriteLine("1NEW VALLLLLLVALLLLLL: " + newVal1);
        ascas1.Parent.CreateDirectory();
        ascas1.WriteAllText(newVal1);

        AbsolutePath ascas = RootDirectory / "samp" / "test.txt";

        if (ascas.FileExists())
        {
            Console.WriteLine("OLD VALLLVALLLLLLVALLLdsdLLLs: " + ascas.ReadAllText());
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
