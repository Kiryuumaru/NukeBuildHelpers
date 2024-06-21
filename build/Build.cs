using Nuke.Common.Tools.DotNet;
using NukeBuildHelpers;
using NukeBuildHelpers.Common.Attributes;
using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Common.Models;
using NukeBuildHelpers.Entry;
using NukeBuildHelpers.Entry.Extensions;
using NukeBuildHelpers.RunContext.Extensions;
using Semver;

class Build : BaseNukeBuildHelpers
{
    public static int Main () => Execute<Build>(x => x.Version);

    public override string[] EnvironmentBranches { get; } = [ "prerelease", "main" ];

    public override string MainEnvironmentBranch { get; } = "main";

    [SecretVariable("NUGET_AUTH_TOKEN")]
    readonly string? NuGetAuthToken;

    [SecretVariable("GITHUB_TOKEN")]
    readonly string? GithubToken;

    TestEntry NugetBuildHelpersTest => _ => _
        .AppId("nuget_build_helpers")
        .Execute(() =>
        {
            DotNetTasks.DotNetClean(_ => _
                .SetProject(RootDirectory / "NukeBuildHelpers.UnitTest" / "NukeBuildHelpers.UnitTest.csproj"));
            DotNetTasks.DotNetTest(_ => _
                .SetProjectFile(RootDirectory / "NukeBuildHelpers.UnitTest" / "NukeBuildHelpers.UnitTest.csproj"));
        });

    BuildEntry NugetBuildHelpersBuild => _ => _
        .AppId("nuget_build_helpers")
        .Execute(context => {
            string version = "0.0.0";
            string? releaseNotes = null;
            if (context.TryGetBumpContext(out var bumpContext))
            {
                version = bumpContext.AppVersion.Version.ToString();
                releaseNotes = bumpContext.AppVersion.ReleaseNotes;
            }
            else if (context.TryGetPullRequestContext(out var pullRequestContext))
            {
                version = pullRequestContext.AppVersion.Version.ToString();
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
                .SetVersion(version)
                .SetPackageReleaseNotes(releaseNotes)
                .SetOutputDirectory(OutputDirectory));
        });

    PublishEntry NugetBuildHelpersPublish => _ => _
        .AppId("nuget_build_helpers")
        .Execute(context =>
        {
            if (context.RunType == RunType.Bump)
            {
                DotNetTasks.DotNetNuGetPush(_ => _
                    .SetSource("https://nuget.pkg.github.com/kiryuumaru/index.json")
                    .SetApiKey(GithubToken)
                    .SetTargetPath(OutputDirectory / "**"));
                DotNetTasks.DotNetNuGetPush(_ => _
                    .SetSource("https://api.nuget.org/v3/index.json")
                    .SetApiKey(NuGetAuthToken)
                    .SetTargetPath(OutputDirectory / "**"));
            }
        });
}
