# BuildEntry Documentation

This document provides an overview of the fluent API functionalities available for `BuildEntry` through the extension methods provided under the namespace `NukeBuildHelpers.Entry.Extensions`.

## Features

- [AppId](#appid)
- [RunnerOS](#runneros)
- [Execute](#execute)
- [CachePath](#cachepath)
- [CacheInvalidator](#cacheinvalidator)
- [Condition](#condition)
- [DisplayName](#displayname)
- [WorkflowBuilder](#workflowbuilder)
- [ReleaseAsset](#releaseasset)
- [CommonReleaseAsset](#commonreleaseasset)

---

## AppId

Sets the app ID of the app. All files created on `OutputDirectory` will propagate on all `PublishEntry` run with the same app ID.

### Definitions

```csharp
IBuildEntryDefinition AppId(string appId);
```

### Usage

* Specify directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .AppId("nuke_build_helpers");
    }
    ```

---

## RunnerOS

Sets the runner OS for the execution. Can choose pre-listed OS under the namespace `NukeBuildHelpers.Runner.Models` or specify custom by overriding the abstract class `RunnerOS`.

### Definitions

```csharp
IBuildEntryDefinition RunnerOS(RunnerOS runnerOS);
IBuildEntryDefinition RunnerOS(Func<RunnerOS> runnerOS);
IBuildEntryDefinition RunnerOS(Func<IRunContext, RunnerOS> runnerOS);
IBuildEntryDefinition RunnerOS(Func<Task<RunnerOS>> runnerOS);
IBuildEntryDefinition RunnerOS(Func<IRunContext, Task<RunnerOS>> runnerOS);
```

### Usage

* Specify to use `RunnerOS.Ubuntu2204` directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;
    using NukeBuildHelpers.Runner.Models;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .RunnerOS(RunnerOS.Ubuntu2204);
    }
    ```

* Resolve at runtime with `IRunContext`

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;
    using NukeBuildHelpers.Runner.Models;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .RunnerOS(context => 
            {
                if (context.RunType == RunType.PullRequest)
                {
                    return RunnerOS.Ubuntu2204;
                }
                else
                {
                    return RunnerOS.Windows2022;
                }
            });
    }
    ```

---

## Execute

Defines the execution that will run at runtime.

### Definitions

```csharp
IBuildEntryDefinition Execute(Func<T> action);
IBuildEntryDefinition Execute(Func<Task> action);
IBuildEntryDefinition Execute(Func<Task<T>> action);
IBuildEntryDefinition Execute(Action<IRunContext> action);
IBuildEntryDefinition Execute(Func<IRunContext, Task> action);
IBuildEntryDefinition Execute(Func<IRunContext, Task<T>> action);
```

### Usage

* Running any plain execution

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .Execute(() =>
            {
                DotNetTasks.DotNetNuGetPush(_ => _
                    .SetSource("https://api.nuget.org/v3/index.json")
                    .SetApiKey(NuGetAuthToken)
                    .SetTargetPath(OutputDirectory / "**"));
            });
    }
    ```

* Running with `IRunContext`

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;
    using NukeBuildHelpers.Common.Enums;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .Execute(context =>
            {
                if (context.RunType == RunType.Bump)
                {
                    DotNetTasks.DotNetNuGetPush(_ => _
                        .SetSource("https://api.nuget.org/v3/index.json")
                        .SetApiKey(NuGetAuthToken)
                        .SetTargetPath(OutputDirectory / "**"));
                }
            });
    }
    ```

---

## CachePath

Sets the paths to cache using `AbsolutePath`.

### Definitions

```csharp
IBuildEntryDefinition CachePath(params AbsolutePath[] cachePath);
IBuildEntryDefinition CachePath(Func<AbsolutePath[]> cachePaths);
IBuildEntryDefinition CachePath(Func<IRunContext, AbsolutePath[]> cachePaths);
IBuildEntryDefinition CachePath(Func<Task<AbsolutePath[]>> cachePaths);
IBuildEntryDefinition CachePath(Func<IRunContext, Task<AbsolutePath[]>> cachePaths);
```

### Usage

* Specify `AbsolutePath` directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .CachePath(RootDirectory / "directoryToCache")
            .CachePath(RootDirectory / "fileToCache.txt");
    }
    ```

* Resolve at runtime with `IRunContext`

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;
    using NukeBuildHelpers.Common.Enums;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .CachePath(context =>
            {
                if (context.RunType == RunType.PullRequest)
                {
                    return RootDirectory / "directoryToCache";
                }
                else
                {
                    return RootDirectory / "fileToCache.txt";
                }
            });
    }
    ```

---

## CacheInvalidator

Sets to invalidate cache if the value is different from the last run.

### Definitions

```csharp
IBuildEntryDefinition CacheInvalidator(string cacheInvalidator);
IBuildEntryDefinition CacheInvalidator(Func<string> cacheInvalidator);
IBuildEntryDefinition CacheInvalidator(Func<IRunContext, string> cacheInvalidator);
IBuildEntryDefinition CacheInvalidator(Func<Task<string>> cacheInvalidator);
IBuildEntryDefinition CacheInvalidator(Func<IRunContext, Task<string>> cacheInvalidator);
```

### Usage

* Specify the value directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .CacheInvalidator("sampleValue");
    }
    ```

* Resolve at runtime with `IRunContext`

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;
    using NukeBuildHelpers.Common.Enums;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .CacheInvalidator(context =>
            {
                if (context.RunType == RunType.Bump)
                {
                    return Guid.NewGuid().ToString();
                }
                else
                {
                    return "sampleValue";
                }
            });
    }
    ```

---

## Condition

Sets the condition to run `Execute`.

### Definitions

```csharp
IBuildEntryDefinition Condition(bool condition);
IBuildEntryDefinition Condition(Func<bool> condition);
IBuildEntryDefinition Condition(Func<IRunContext, bool> condition);
IBuildEntryDefinition Condition(Func<Task<bool>> condition);
IBuildEntryDefinition Condition(Func<IRunContext, Task<bool>> condition);
```

### Usage

* Specify `bool` directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .Condition(false);
    }
    ```

* Resolve at runtime with `IRunContext`

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;
    using NukeBuildHelpers.Common.Enums;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .Condition(context =>
            {
                return context.RunType == RunType.Bump;
            });
    }
    ```

---

## DisplayName

Sets the display name of the entry. Modifying the value will need to rebuild the workflow.

### Definitions

```csharp
IBuildEntryDefinition DisplayName(string displayName);
IBuildEntryDefinition DisplayName(Func<string> displayName);
IBuildEntryDefinition DisplayName(Func<Task<string>> displayName);
```

### Usage

* Specify `string` directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .DisplayName("Test Entry Sample");
    }
    ```

---

## WorkflowBuilder

Sets custom workflow tasks or steps on any supported pipelines. Modifying the value will need to rebuild the workflow.

### Definitions

```csharp
IBuildEntryDefinition WorkflowBuilder(Action<IWorkflowBuilder> workflowBuilder);
IBuildEntryDefinition WorkflowBuilder(Func<IWorkflowBuilder, Task> workflowBuilder);
IBuildEntryDefinition WorkflowBuilder(Func<IWorkflowBuilder, Task<T>> workflowBuilder);
```

### Usage

* Specify directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
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
            });
    }
    ```
    
---

## ReleaseAsset

Sets the `AbsolutePath` to release on git release as an asset.

### Definitions

```csharp
IBuildEntryDefinition ReleaseAsset(params AbsolutePath[] assets);
IBuildEntryDefinition ReleaseAsset(Func<AbsolutePath[]> assets);
IBuildEntryDefinition ReleaseAsset(Func<IRunContext, AbsolutePath[]> assets);
IBuildEntryDefinition ReleaseAsset(Func<Task<AbsolutePath[]>> assets);
IBuildEntryDefinition ReleaseAsset(Func<IRunContext, Task<AbsolutePath[]>> assets);
```

### Usage

* Specify `AbsolutePath` file directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .ReleaseAsset(OutputDirectory / "fileAsset.zip");
    }
    ```

* Specify `AbsolutePath` folder directly to zip on release

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .ReleaseAsset(OutputDirectory / "assets");
    }
    ```
    
---

## CommonReleaseAsset

Sets the `AbsolutePath` to release on git release as a common asset. All assets created on all `BuildEntry` with the same app ID will be bundled together as a single zip archive named `<appId>-<version>.zip` (e.g., `nuke_build_helpers-4.0.4+build.407.zip`).

### Definitions

```csharp
IBuildEntryDefinition CommonReleaseAsset(params AbsolutePath[] assets);
IBuildEntryDefinition CommonReleaseAsset(Func<AbsolutePath[]> assets);
IBuildEntryDefinition CommonReleaseAsset(Func<IRunContext, AbsolutePath[]> assets);
IBuildEntryDefinition CommonReleaseAsset(Func<Task<AbsolutePath[]>> assets);
IBuildEntryDefinition CommonReleaseAsset(Func<IRunContext, Task<AbsolutePath[]>> assets);
```

### Usage

* Specify `AbsolutePath` file directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .CommonReleaseAsset(OutputDirectory / "fileAsset.txt");
    }
    ```

* Specify `AbsolutePath` folder directly to zip on release

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .CommonReleaseAsset(OutputDirectory / "assets");
    }
    ```
    
#AppId