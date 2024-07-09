# PublishEntry Documentation

This document provides an overview of the fluent API functionalities available for `PublishEntry` through the extension methods provided under the namespace `NukeBuildHelpers.Entry.Extensions`.

## Features

- [AppId](#appid)
- [RunnerOS](#runneros)
- [Execute](#execute)
- [CachePath](#cachepath)
- [CacheInvalidator](#cacheinvalidator)
- [Condition](#condition)
- [DisplayName](#displayname)
- [WorkflowBuilder](#workflowbuilder)

---

## AppId

Sets the app ID of the app. All files created on `OutputDirectory` under all `BuildEntry` with the same app ID will propagate on this run.

### Definitions

```csharp
IPublishEntryDefinition AppId(string appId);
```

### Usage

* Specify directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        PublishEntry SamplePublishEntry => _ => _
            .AppId("nuke_build_helpers");
    }
    ```

---

## RunnerOS

Sets the runner OS for the execution. Can choose pre-listed OS under the namespace `NukeBuildHelpers.Runner.Models` or specify custom by overriding the abstract class `RunnerOS`.

### Definitions

```csharp
IPublishEntryDefinition RunnerOS(RunnerOS runnerOS);
IPublishEntryDefinition RunnerOS(Func<RunnerOS> runnerOS);
IPublishEntryDefinition RunnerOS(Func<IRunContext, RunnerOS> runnerOS);
IPublishEntryDefinition RunnerOS(Func<Task<RunnerOS>> runnerOS);
IPublishEntryDefinition RunnerOS(Func<IRunContext, Task<RunnerOS>> runnerOS);
```

### Usage

* Specify to use `RunnerOS.Ubuntu2204` directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;
    using NukeBuildHelpers.Runner.Models;

    class Build : BaseNukeBuildHelpers
    {
        ...

        PublishEntry SamplePublishEntry => _ => _
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

        PublishEntry SamplePublishEntry => _ => _
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
IPublishEntryDefinition Execute(Func<T> action);
IPublishEntryDefinition Execute(Func<Task> action);
IPublishEntryDefinition Execute(Func<Task<T>> action);
IPublishEntryDefinition Execute(Action<IRunContext> action);
IPublishEntryDefinition Execute(Func<IRunContext, Task> action);
IPublishEntryDefinition Execute(Func<IRunContext, Task<T>> action);
```

### Usage

* Running any plain execution

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        PublishEntry SamplePublishEntry => _ => _
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

        PublishEntry SamplePublishEntry => _ => _
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
IPublishEntryDefinition CachePath(params AbsolutePath[] cachePath);
IPublishEntryDefinition CachePath(Func<AbsolutePath[]> cachePaths);
IPublishEntryDefinition CachePath(Func<IRunContext, AbsolutePath[]> cachePaths);
IPublishEntryDefinition CachePath(Func<Task<AbsolutePath[]>> cachePaths);
IPublishEntryDefinition CachePath(Func<IRunContext, Task<AbsolutePath[]>> cachePaths);
```

### Usage

* Specify `AbsolutePath` directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        PublishEntry SamplePublishEntry => _ => _
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

        PublishEntry SamplePublishEntry => _ => _
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
IPublishEntryDefinition CacheInvalidator(string cacheInvalidator);
IPublishEntryDefinition CacheInvalidator(Func<string> cacheInvalidator);
IPublishEntryDefinition CacheInvalidator(Func<IRunContext, string> cacheInvalidator);
IPublishEntryDefinition CacheInvalidator(Func<Task<string>> cacheInvalidator);
IPublishEntryDefinition CacheInvalidator(Func<IRunContext, Task<string>> cacheInvalidator);
```

### Usage

* Specify the value directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        PublishEntry SamplePublishEntry => _ => _
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

        PublishEntry SamplePublishEntry => _ => _
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
IPublishEntryDefinition Condition(bool condition);
IPublishEntryDefinition Condition(Func<bool> condition);
IPublishEntryDefinition Condition(Func<IRunContext, bool> condition);
IPublishEntryDefinition Condition(Func<Task<bool>> condition);
IPublishEntryDefinition Condition(Func<IRunContext, Task<bool>> condition);
```

### Usage

* Specify `bool` directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        PublishEntry SamplePublishEntry => _ => _
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

        PublishEntry SamplePublishEntry => _ => _
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
IPublishEntryDefinition DisplayName(string displayName);
IPublishEntryDefinition DisplayName(Func<string> displayName);
IPublishEntryDefinition DisplayName(Func<Task<string>> displayName);
```

### Usage

* Specify `string` directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        PublishEntry SamplePublishEntry => _ => _
            .DisplayName("Test Entry Sample");
    }
    ```

---

## WorkflowBuilder

Sets custom workflow tasks or steps on any supported pipelines. Modifying the value will need to rebuild the workflow.

### Definitions

```csharp
IPublishEntryDefinition WorkflowBuilder(Action<IWorkflowBuilder> workflowBuilder);
IPublishEntryDefinition WorkflowBuilder(Func<IWorkflowBuilder, Task> workflowBuilder);
IPublishEntryDefinition WorkflowBuilder(Func<IWorkflowBuilder, Task<T>> workflowBuilder);
```

### Usage

* Specify directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        PublishEntry SamplePublishEntry => _ => _
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
