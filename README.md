# NukeBuildHelpers

NukeBuildHelpers is a C# project build automation tool built on top of NukeBuild. It supports both GitHub Actions and Azure Pipelines for CI/CD, enabling release management across multiple projects and environments within a single repository.

## Features

- **Multi-project and Multi-environment Support**: Handle releases for multiple projects and environments in a single repository.
- **CI/CD Integration**: Generate GitHub Actions and Azure Pipelines workflows.
- **Automated Versioning**: Interactive CLI for bumping project versions with validation.
- **Flexible Build Flow**: Override abstract classes to create custom build flows.

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

### Creating Build Flows

To create custom build flows, override the abstract classes `AppEntry` or `AppTestEntry`.

#### Example `AppEntry` Implementation

```csharp
namespace _build;

public class NugetBuildHelpers : AppEntry<Build>
{
    public override RunsOnType BuildRunsOn => RunsOnType.Ubuntu2204;
    public override RunsOnType PublishRunsOn => RunsOnType.Ubuntu2204;

    [SecretHelper("NUGET_AUTH_TOKEN")]
    readonly string? NuGetAuthToken;

    [SecretHelper("GITHUB_TOKEN")]
    readonly string? GithubToken;

    public override void Build(AppRunContext appRunContext)
    {
        // Build logic here
    }

    public override void Publish(AppRunContext appRunContext)
    {
        // Publish logic here
    }
}
```

#### Example `AppTestEntry` Implementation

```csharp
namespace _build;

public class NugetBuildHelpersTest : AppTestEntry<Build>
{
    public override RunsOnType RunsOn => RunsOnType.WindowsLatest;
    public override Type[] AppEntryTargets => [typeof(NugetBuildHelpers)];

    public override void Run(AppTestRunContext appTestRunContext)
    {
        // Test logic here
    }
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
╬══════════════════════╬═════════════╬════════════════════╬═════════════════════╬
║        App Id        ║ Environment ║   Bumped Version   ║      Published      ║
╬══════════════════════╬═════════════╬════════════════════╬═════════════════════╬
║ nuget_build_helpers  ║ prerelease  ║ 2.1.0-prerelease.1 ║ 2.0.0-prerelease.8* ║
║                      ║    main     ║ 2.0.0              ║         yes         ║
║----------------------║-------------║--------------------║---------------------║
║ nuget_build_helpers2 ║ prerelease  ║ 0.1.0-prerelease.2 ║         no          ║
╬══════════════════════╬═════════════╬════════════════════╬═════════════════════╬
```

- The `StatusWatch` subcommand continuously monitors the version status. Example output from the subcommand:
```
╬══════════════════════╬═════════════╬════════════════════╬════════════╬
║        App Id        ║ Environment ║      Version       ║   Status   ║
╬══════════════════════╬═════════════╬════════════════════╬════════════╬
║ nuget_build_helpers  ║ prerelease  ║ 2.1.0-prerelease.2 ║  Published ║
║                      ║    main     ║ 2.0.0              ║  Published ║
║----------------------║-------------║--------------------║------------║
║ nuget_build_helpers2 ║ prerelease  ║ 0.1.0-prerelease.2 ║ Run Failed ║
╬══════════════════════╬═════════════╬════════════════════╬════════════╬
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
