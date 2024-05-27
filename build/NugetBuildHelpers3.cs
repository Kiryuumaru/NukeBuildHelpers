using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using NukeBuildHelpers;
using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Models.RunContext;
using Serilog;

namespace _build;

class NugetBuildHelpers3 : AppEntry<Build>
{
    public override RunsOnType BuildRunsOn => RunsOnType.Windows2022;

    public override RunsOnType PublishRunsOn => RunsOnType.UbuntuLatest;

    public override RunType RunBuildOn => RunType.Commit;

    public override RunType RunPublishOn => RunType.Commit;

    public override bool MainRelease => false;

    public override bool RunParallel => false;

    public override void Build(AppRunContext appRunContext)
    {
        AppVersion? appVersion = null;
        if (appRunContext is AppPipelineRunContext appPipelineRunContext)
        {
            appVersion = appPipelineRunContext.AppVersion;
        }
        OutputDirectory.DeleteDirectory();
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

    public override void Publish(AppRunContext appRunContext)
    {
        if (appRunContext.RunType == RunType.Bump)
        {
            foreach (var ss in OutputDirectory.GetFiles())
            {
                Log.Information("Publish: {name}", ss.Name);
            }
        }
    }
}
