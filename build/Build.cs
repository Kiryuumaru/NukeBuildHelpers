using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using NukeBuildHelpers;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Common.Attributes;
using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Entry;
using NukeBuildHelpers.Entry.Extensions;
using NukeBuildHelpers.Pipelines.Azure.Extensions;
using NukeBuildHelpers.Pipelines.Github.Extensions;
using NukeBuildHelpers.RunContext.Extensions;
using NukeBuildHelpers.Runner.Abstraction;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

class Build : BaseNukeBuildHelpers
{
    public static int Main() => Execute<Build>(x => x.Interactive);

    public override string[] EnvironmentBranches { get; } = ["prerelease", "main"];

    public override string MainEnvironmentBranch { get; } = "main";

    [SecretVariable("NUGET_AUTH_TOKEN")]
    readonly string? NuGetAuthToken;

    [SecretVariable("GITHUB_TOKEN")]
    readonly string? GithubToken;

    protected override WorkflowConfigEntry WorkflowConfig => _ => _
        .PreSetupRunnerOS(RunnerOS.Windows2022)
        .PostSetupRunnerOS(RunnerOS.Ubuntu2204);

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
        .ExecuteBeforeBuild(true)
        .CachePath(RootDirectory / "testCache", RootDirectory / "testFile.txt")
        .CacheInvalidator("1")
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
        .Execute(async context =>
        {
            var (NukeBuildHelpersProjectPath, NukeBuildHelpersProjectTestPath) = await PrepareClonedProjects(nameof(NukeBuildHelpersTest1));

            var testDirFilePath = RootDirectory / "testCache" / "testFile.txt";
            var testFilePath = RootDirectory / "testFile.txt";
            testDirFilePath.Parent.CreateDirectory();
            testFilePath.Parent.CreateDirectory();
            string oldValDir = testDirFilePath.FileExists() ? testDirFilePath.ReadAllText() : "";
            string oldValFile = testFilePath.FileExists() ? testFilePath.ReadAllText() : "";
            Log.Information("Cache old value dir: {oldVal}", oldValDir);
            Log.Information("Cache old value file: {oldVal}", oldValFile);
            string testDirFile = Guid.NewGuid().ToString();
            string testFile = Guid.NewGuid().ToString();
            Log.Information("Cache new value dir: {newVal}", testDirFile);
            Log.Information("Cache new value file: {newVal}", testFile);
            testDirFilePath.WriteAllText(testDirFile);
            testFilePath.WriteAllText(testFile);

            DotNetTasks.DotNetClean(_ => _
                .SetProject(NukeBuildHelpersProjectTestPath));
            DotNetTasks.DotNetTest(_ => _
                .SetProjectFile(NukeBuildHelpersProjectTestPath));
        });

    TestEntry NukeBuildHelpersTest2 => _ => _
        .AppId("nuke_build_helpers")
        .WorkflowId("NukeBuildHelpersTest2CustomId")
        .DisplayName("Test try 2")
        .RunnerOS(RunnerOS.Windows2022)
        .ExecuteBeforeBuild(true)
        .Execute(async () =>
        {
            var (NukeBuildHelpersProjectPath, NukeBuildHelpersProjectTestPath) = await PrepareClonedProjects(nameof(NukeBuildHelpersTest2));

            DotNetTasks.DotNetClean(_ => _
                .SetProject(NukeBuildHelpersProjectTestPath));
            DotNetTasks.DotNetTest(_ => _
                .SetProjectFile(NukeBuildHelpersProjectTestPath));
        });

    TestEntry NukeBuildHelpersTest3 => _ => _
        .AppId("nuke_build_helpers")
        .DisplayName("Test try 3")
        .RunnerOS(RunnerOS.Windows2022)
        .Execute(async () =>
        {
            var (NukeBuildHelpersProjectPath, NukeBuildHelpersProjectTestPath) = await PrepareClonedProjects(nameof(NukeBuildHelpersTest3));

            foreach (var path in OutputDirectory.GetFiles("**", 99))
            {
                Log.Information(path);
            }
            DotNetTasks.DotNetClean(_ => _
                .SetProject(NukeBuildHelpersProjectTestPath));
            DotNetTasks.DotNetTest(_ => _
                .SetProjectFile(NukeBuildHelpersProjectTestPath));
        });

    TestEntry NukeBuildHelpersTest4 => _ => _
        .AppId("nuke_build_helpers2")
        .DisplayName("Test try 4")
        .RunnerOS(RunnerOS.Windows2022)
        .Execute(async () =>
        {
            var (NukeBuildHelpersProjectPath, NukeBuildHelpersProjectTestPath) = await PrepareClonedProjects(nameof(NukeBuildHelpersTest4));

            foreach (var path in OutputDirectory.GetFiles("**", 99))
            {
                Log.Information(path);
            }
            DotNetTasks.DotNetClean(_ => _
                .SetProject(NukeBuildHelpersProjectTestPath));
            DotNetTasks.DotNetTest(_ => _
                .SetProjectFile(NukeBuildHelpersProjectTestPath));
        });

    TestEntry NukeBuildHelpersTest5 => _ => _
        .DisplayName("Test try 5")
        .RunnerOS(RunnerOS.Windows2022)
        .Execute(async () =>
        {
            var (NukeBuildHelpersProjectPath, NukeBuildHelpersProjectTestPath) = await PrepareClonedProjects(nameof(NukeBuildHelpersTest5));

            foreach (var path in OutputDirectory.GetFiles("**", 99))
            {
                Log.Information(path);
            }
            DotNetTasks.DotNetClean(_ => _
                .SetProject(NukeBuildHelpersProjectTestPath));
            DotNetTasks.DotNetTest(_ => _
                .SetProjectFile(NukeBuildHelpersProjectTestPath));
        });

    BuildEntry NukeBuildHelpersBuild1 => _ => _
        .AppId("nuke_build_helpers")
        .DisplayName("Build main")
        .RunnerOS(RunnerOS.Ubuntu2204)
        .Execute(async context =>
        {
            var (NukeBuildHelpersProjectPath, NukeBuildHelpersProjectTestPath) = await PrepareClonedProjects(nameof(NukeBuildHelpersBuild1));

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
                .SetProject(NukeBuildHelpersProjectPath));
            DotNetTasks.DotNetBuild(_ => _
                .SetProjectFile(NukeBuildHelpersProjectPath)
                .SetConfiguration("Release"));
            DotNetTasks.DotNetPack(_ => _
                .SetProject(NukeBuildHelpersProjectPath)
                .SetConfiguration("Release")
                .SetNoRestore(true)
                .SetNoBuild(true)
                .SetIncludeSymbols(true)
                .SetSymbolPackageFormat("snupkg")
                .SetVersion(version)
                .SetPackageReleaseNotes(NormalizeReleaseNotes(releaseNotes))
                .SetOutputDirectory(OutputDirectory / "main"));
        });

    BuildEntry NukeBuildHelpersBuild2 => _ => _
        .AppId("nuke_build_helpers")
        .DisplayName("Build try")
        .RunnerOS(RunnerOS.Windows2022)
        .Matrix([("Mat1", 1), ("Mat2", 1)], (definition1, matrix1) =>
        {
            definition1.Matrix([("Mat3", 1), ("Mat4", 1)], (definition2, matrix2) =>
            {
                definition2.WorkflowId($"NukeBuildHelpersBuild2{matrix1.Item1}{matrix2.Item1}");
                definition2.DisplayName("Build try " + matrix1.Item1 + " sub " + matrix2.Item1);
                definition2.Execute(() =>
                {
                    Log.Information("I am hereeee: {s}", matrix1.Item1 + " sub " + matrix2.Item1);
                });
            });
        })
        .Execute(async context =>
        {
            var (NukeBuildHelpersProjectPath, NukeBuildHelpersProjectTestPath) = await PrepareClonedProjects(nameof(NukeBuildHelpersBuild2));

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
                .SetProject(NukeBuildHelpersProjectPath));
            DotNetTasks.DotNetBuild(_ => _
                .SetProjectFile(NukeBuildHelpersProjectPath)
                .SetConfiguration("Release"));
            DotNetTasks.DotNetPack(_ => _
                .SetProject(NukeBuildHelpersProjectPath)
                .SetConfiguration("Release")
                .SetNoRestore(true)
                .SetNoBuild(true)
                .SetIncludeSymbols(true)
                .SetSymbolPackageFormat("snupkg")
                .SetVersion(version)
                .SetPackageReleaseNotes(NormalizeReleaseNotes(releaseNotes))
                .SetOutputDirectory(OutputDirectory / "try" / "test_release"));
            (OutputDirectory / "try" / "test_release").TarGZipTo(OutputDirectory / "try" / "test_release.tar.gz");
        });

    BuildEntry NukeBuildHelpersBuild3 => _ => _
        .AppId("nuke_build_helpers2")
        .DisplayName("Build try 2")
        .RunnerOS(RunnerOS.Windows2022)
        .Execute(async context =>
        {
            var (NukeBuildHelpersProjectPath, NukeBuildHelpersProjectTestPath) = await PrepareClonedProjects(nameof(NukeBuildHelpersBuild3));

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
                .SetProject(NukeBuildHelpersProjectPath));
            DotNetTasks.DotNetBuild(_ => _
                .SetProjectFile(NukeBuildHelpersProjectPath)
                .SetConfiguration("Release"));
            DotNetTasks.DotNetPack(_ => _
                .SetProject(NukeBuildHelpersProjectPath)
                .SetConfiguration("Release")
                .SetNoRestore(true)
                .SetNoBuild(true)
                .SetIncludeSymbols(true)
                .SetSymbolPackageFormat("snupkg")
                .SetVersion(version)
                .SetPackageReleaseNotes(NormalizeReleaseNotes(releaseNotes))
                .SetOutputDirectory(OutputDirectory / "test_release 2"));
        });

    PublishEntry NukeBuildHelpersPublish => _ => _
        .AppId("nuke_build_helpers")
        .RunnerOS(RunnerOS.Ubuntu2204)
        .ReleaseAsset(OutputDirectory / "try" / "test_release")
        .ReleaseAsset(OutputDirectory / "try" / "test_release.tar.gz")
        .ReleaseCommonAsset(OutputDirectory / "main")
        .Execute(async context =>
        {
            var (NukeBuildHelpersProjectPath, NukeBuildHelpersProjectTestPath) = await PrepareClonedProjects(nameof(NukeBuildHelpersPublish));

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

    private async Task<(AbsolutePath NukeBuildHelpersProjectPath, AbsolutePath NukeBuildHelpersProjectTestPath)> PrepareClonedProjects(string subPath)
    {
        var clonedProjectsDir = TemporaryDirectory / "ClonedProjects"/ subPath;
        await clonedProjectsDir.Delete();
        var nukeBuildHelpersPath = clonedProjectsDir / "NukeBuildHelpers" / "NukeBuildHelpers.csproj";
        var nukeBuildHelpersTestPath = clonedProjectsDir / "NukeBuildHelpers.UnitTest" / "NukeBuildHelpers.UnitTest.csproj";
        await (RootDirectory / "NukeBuildHelpers.sln").CopyTo(clonedProjectsDir / "NukeBuildHelpers.sln");
        await (RootDirectory / "global.json").CopyTo(clonedProjectsDir / "global.json");
        await (RootDirectory / "LICENSE").CopyTo(clonedProjectsDir / "LICENSE");
        await (RootDirectory / "README.md").CopyTo(clonedProjectsDir / "README.md");
        await (RootDirectory / "NukeBuildHelpers").CopyTo(nukeBuildHelpersPath.Parent);
        await (RootDirectory / "NukeBuildHelpers.UnitTest").CopyTo(nukeBuildHelpersTestPath.Parent);
        await  (nukeBuildHelpersPath.Parent / "bin").Delete();
        await  (nukeBuildHelpersTestPath.Parent / "obj").Delete();
        return (nukeBuildHelpersPath, nukeBuildHelpersTestPath);
    }

    private string? NormalizeReleaseNotes(string? releaseNotes)
    {
        return releaseNotes?
            .Replace(",", "%2C")?
            .Replace(":", "%3A")?
            .Replace(";", "%3B");
    }
}
