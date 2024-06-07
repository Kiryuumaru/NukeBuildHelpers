using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.NuGet;
using NukeBuildHelpers;
using NukeBuildHelpers.Attributes;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Models;
using System;

namespace _build;

class NugetBuildHelpers_Build : BuildEntry<Build>
{
    public override string Id => "nuget_build_helpers";

    public override RunnerOS RunnerOS => RunnerOS.Ubuntu2204;

    public override RunType RunOn => RunType.All;

    public override void Run(AppRunContext runContext)
    {
        AppVersion? appVersion = null;
        if (runContext is AppPipelineRunContext appPipelineRunContext)
        {
            appVersion = appPipelineRunContext.AppVersion;
        }
        DotNetTasks.DotNetClean(_ => _
            .SetProject(RootDirectory / "NukeBuildHelpers" / "NukeBuildHelpers.csproj"));
        DotNetTasks.DotNetBuild(_ => _
            .SetProjectFile(RootDirectory / "NukeBuildHelpers" / "NukeBuildHelpers.csproj")
            .SetConfiguration("Release"));
        DotNetTasks.DotNetPack(_ => _
            .SetProject(RootDirectory / "NukeBuildHelpers" / "NukeBuildHelpers.csproj")
            .SetConfiguration("Release")
            .SetNoRestore(true)
            .SetNoBuild(true)
            .SetIncludeSymbols(true)
            .SetSymbolPackageFormat("snupkg")
            .SetVersion(appVersion?.Version?.ToString() ?? "0.0.0")
            .SetPackageReleaseNotes(appVersion?.ReleaseNotes)
            .SetOutputDirectory(OutputDirectory));
    }
}
