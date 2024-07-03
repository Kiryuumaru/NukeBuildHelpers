# NukeBuildHelpers

NukeBuildHelpers is a C# project build automation tool built on top of NukeBuild. It supports both GitHub Actions and Azure Pipelines for CI/CD, enabling release management across multiple projects and environments within a single repository.

**NuGet**

|Name|Info|
| ------------------- | :------------------: |
|NukeBuildHelpers|[![NuGet](https://buildstats.info/nuget/NukeBuildHelpers?includePreReleases=true)](https://www.nuget.org/packages/NukeBuildHelpers/)|

## Features

- **Multi-project and Multi-environment Support**: Handle releases for multiple projects and environments in a single repository.
- **CI/CD Integration**: Generate GitHub Actions and Azure Pipelines workflows.
- **Automated Versioning**: Interactive CLI for bumping project versions with validation.
- **Flexible Build Flow**: Implement the target entries to create custom build flows.

## Quick Start

### Using the Repository Template

To quickly set up a new project, use the [NukeBuildTemplate](https://github.com/Kiryuumaru/NukeBuildTemplate) repository template:

1. Clone the template repository.
2. Follow the setup instructions in the template.

### Installing via NuGet

If you already have a NukeBuild setup, you can install NukeBuildHelpers via NuGet:

```sh
dotnet add package NukeBuildHelpers
```

## Usage

### Preparing `Build` class

1. Change the base class from `NukeBuild` to `BaseNukeBuildHelpers`:

    ```csharp
    class Build : BaseNukeBuildHelpers
    {
        ...
    }
    ```

2. Add your environment branches:

    ```csharp
    class Build : BaseNukeBuildHelpers
    {
        ...

        public override string[] EnvironmentBranches { get; } = [ "prerelease", "master" ];

        public override string MainEnvironmentBranch { get; } = "master";
    }
    ```

### Creating Build Flows

To create custom build flows, implement any of the target entries `TestEntry`, `BuildEntry` or `PublishEntry`.

#### Example `TestEntry` Implementation

```csharp
class Build : BaseNukeBuildHelpers
{
    ...

    TestEntry NukeBuildHelpersTest => _ => _
        .AppId("nuke_build_helpers")
        .RunnerOS(RunnerOS.Ubuntu2204)
        .Execute(() =>
        {
            DotNetTasks.DotNetClean(_ => _
                .SetProject(RootDirectory / "NukeBuildHelpers.UnitTest" / "NukeBuildHelpers.UnitTest.csproj"));
            DotNetTasks.DotNetTest(_ => _
                .SetProjectFile(RootDirectory / "NukeBuildHelpers.UnitTest" / "NukeBuildHelpers.UnitTest.csproj"));
        });
}
```

#### Example `BuildEntry` Implementation

```csharp
class Build : BaseNukeBuildHelpers
{
    ...

    BuildEntry NukeBuildHelpersBuild => _ => _
        .AppId("nuke_build_helpers")
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
}
```

#### Example `PublishEntry` Implementation

```csharp
class Build : BaseNukeBuildHelpers
{
    ...

    PublishEntry NukeBuildHelpersPublish => _ => _
        .AppId("nuke_build_helpers")
        .RunnerOS(RunnerOS.Ubuntu2204)
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
```

### Generating Workflows

Generate GitHub and Azure Pipelines workflows using CLI commands:

```sh
# Generate GitHub workflow
build githubworkflow

# Generate Azure Pipelines workflow
build azureworkflow
```

These commands will generate `azure-pipelines.yml` and `.github/workflows/nuke-cicd.yml` respectively.

### Bumping Project Version

Use the `build bump` command to interactively bump the project version:

```sh
build bump
```

### CLI Subcommands

- `Fetch`: Fetch git commits and tags.
- `Version`: Show the current version from all releases.
- `Bump`: Interactive, bump the version by validating and tagging.
- `BumpAndForget`: Interactive, bump and forget the version by validating and tagging.
- `StatusWatch`: Show the current version status from all releases.
- `Test`: Run tests.
- `Build`: Build the project.
- `Publish`: Publish the project.
- `GithubWorkflow`: Build the CI/CD workflow for GitHub.
- `AzureWorkflow`: Build the CI/CD workflow for Azure.

## Versioning and Status

- The `Version` subcommand shows the current version from all releases. Example output from the subcommand:

```
╬═════════════════════╬═════════════╬════════════════════╬═════════════════════╬
║        App Id       ║ Environment ║   Bumped Version   ║      Published      ║
╬═════════════════════╬═════════════╬════════════════════╬═════════════════════╬
║ nuke_build_helpers  ║ prerelease  ║ 2.1.0-prerelease.1 ║ 2.0.0-prerelease.8* ║
║                     ║   master    ║ 2.0.0              ║         yes         ║
║---------------------║-------------║--------------------║---------------------║
║ nuke_build_helpers2 ║ prerelease  ║ 0.1.0-prerelease.2 ║         no          ║
║                     ║   master    ║ -                  ║         no          ║
╬═════════════════════╬═════════════╬════════════════════╬═════════════════════╬
```

- The `StatusWatch` subcommand continuously monitors the version status. Example output from the subcommand:
```
╬═════════════════════╬═════════════╬════════════════════╬═══════════════╬
║        App Id       ║ Environment ║      Version       ║    Status     ║
╬═════════════════════╬═════════════╬════════════════════╬═══════════════╬
║ nuke_build_helpers  ║ prerelease  ║ 2.1.0-prerelease.2 ║   Published   ║
║                     ║   master    ║ 2.0.0              ║   Published   ║
║---------------------║-------------║--------------------║---------------║
║ nuke_build_helpers2 ║ prerelease  ║ 0.1.0-prerelease.2 ║  Run Failed   ║
║                     ║   master    ║ -                  ║ Not published ║
╬═════════════════════╬═════════════╬════════════════════╬═══════════════╬
```

Status types include:

- **Run Failed:** The build encountered an error and did not complete successfully.
- **Published:** The build was successfully published.
- **Publishing:** The build is currently in the process of being published.
- **Waiting for Queue:** The build is waiting in the queue to be processed.
- **Not Published:** The build has not been published.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgements

- [NukeBuild](https://nuke.build/) for providing the foundation for this project.
