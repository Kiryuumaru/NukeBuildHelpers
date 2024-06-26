using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using NukeBuildHelpers;
using NukeBuildHelpers.Common.Attributes;
using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Entry;
using NukeBuildHelpers.Entry.Extensions;
using NukeBuildHelpers.Pipelines.Azure.Extensions;
using NukeBuildHelpers.Pipelines.Github.Extensions;
using NukeBuildHelpers.RunContext.Extensions;
using NukeBuildHelpers.Runner.Abstraction;
using Serilog;
using System.Collections.Generic;
using System.Linq;

class Build : BaseNukeBuildHelpers
{
    public static int Main () => Execute<Build>(x => x.Version);

    public override string[] EnvironmentBranches { get; } = [ "prerelease", "main" ];

    public override string MainEnvironmentBranch { get; } = "main";

    [SecretVariable("NUGET_AUTH_TOKEN")]
    readonly string? NuGetAuthToken;

    [SecretVariable("GITHUB_TOKEN")]
    readonly string? GithubToken;

    Target Clean => _ => _
        .Executes(() =>
        {
            foreach (var path in RootDirectory.GetFiles("**", 99).Where(i => i.Name.EndsWith(".csproj")))
            {
                if (path.Name == "_build.csproj")
                {
                    continue;
                }
                Log.Information("Cleaning {path}", path);
                (path.Parent / "bin").DeleteDirectory();
                (path.Parent / "obj").DeleteDirectory();
            }
            (RootDirectory / ".vs").DeleteDirectory();
        });

    TestEntry NukeBuildHelpersTest1 => _ => _
        .AppId("nuke_build_helpers")
        .DisplayName("Test try 1")
        .RunnerOS(RunnerOS.Ubuntu2204)
        .WorkflowBuilder(builder =>
        {
            if (builder.TryGetGithubWorkflowBuilder(out var githubWorkflowBuilder))
            {
                githubWorkflowBuilder.AddPostExecuteStep(new Dictionary<string, object>()
                {
                    { "id", "test_github_2" },
                    { "name", "Custom github step test 2" },
                    { "run", "echo \"Test github 2\"" },
                });
                githubWorkflowBuilder.AddPreExecuteStep(new Dictionary<string, object>()
                {
                    { "id", "test_github_1" },
                    { "name", "Custom github step test 1" },
                    { "run", "echo \"Test github 1\"" },
                });
            }
            if (builder.TryGetAzureWorkflowBuilder(out var azureWorkflowBuilder))
            {
                azureWorkflowBuilder.AddPostExecuteStep(new Dictionary<string, object>()
                {
                    { "script", "echo \"Test azure 2\"" },
                    { "name", "test_azure_2" },
                    { "displayName", "Custom azure step test 2" },
                });
                azureWorkflowBuilder.AddPreExecuteStep(new Dictionary<string, object>()
                {
                    { "script", "echo \"Test azure 1\"" },
                    { "name", "test_azure_1" },
                    { "displayName", "Custom azure step test 1" },
                });
            }
        })
        .Execute(() =>
        {
            DotNetTasks.DotNetClean(_ => _
                .SetProject(RootDirectory / "NukeBuildHelpers.UnitTest" / "NukeBuildHelpers.UnitTest.csproj"));
            DotNetTasks.DotNetTest(_ => _
                .SetProjectFile(RootDirectory / "NukeBuildHelpers.UnitTest" / "NukeBuildHelpers.UnitTest.csproj"));
        });

    TestEntry NukeBuildHelpersTest2 => _ => _
        .AppId("nuke_build_helpers")
        .DisplayName("Test try 2")
        .RunnerOS(RunnerOS.Windows2022)
        .Execute(() =>
        {
            DotNetTasks.DotNetClean(_ => _
                .SetProject(RootDirectory / "NukeBuildHelpers.UnitTest" / "NukeBuildHelpers.UnitTest.csproj"));
            DotNetTasks.DotNetTest(_ => _
                .SetProjectFile(RootDirectory / "NukeBuildHelpers.UnitTest" / "NukeBuildHelpers.UnitTest.csproj"));
        });

    BuildEntry NukeBuildHelpersBuild1 => _ => _
        .AppId("nuke_build_helpers")
        .DisplayName("Build main")
        .RunnerOS(RunnerOS.Ubuntu2204)
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
                .SetOutputDirectory(OutputDirectory / "main"));
        });

    BuildEntry NukeBuildHelpersBuild2 => _ => _
        .AppId("nuke_build_helpers")
        .DisplayName("Build try")
        .RunnerOS(RunnerOS.Windows2022)
        .Condition(true)
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
                .SetOutputDirectory(OutputDirectory / "try" / "awdawd"));
        });

    BuildEntry NukeBuildHelpersBuild3 => _ => _
        .AppId("nuke_build_helpers2")
        .DisplayName("Build try 2")
        .RunnerOS(RunnerOS.Windows2022)
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
                .SetOutputDirectory(OutputDirectory / "try 2"));
        });

    PublishEntry NukeBuildHelpersPublish => _ => _
        .AppId("nuke_build_helpers")
        .RunnerOS(RunnerOS.Ubuntu2204)
        .Condition(true)
        .Execute(context =>
        {
            foreach (var path in OutputDirectory.GetFiles("**", 99))
            {
                Log.Information(path);
            }
            if (context.RunType == RunType.Bump)
            {
                DotNetTasks.DotNetNuGetPush(_ => _
                    .SetSource("https://nuget.pkg.github.com/kiryuumaru/index.json")
                    .SetApiKey(GithubToken)
                    .SetTargetPath(OutputDirectory / "main" / "**"));
                DotNetTasks.DotNetNuGetPush(_ => _
                    .SetSource("https://api.nuget.org/v3/index.json")
                    .SetApiKey(NuGetAuthToken)
                    .SetTargetPath(OutputDirectory / "main" / "**"));
            }
        });
}
