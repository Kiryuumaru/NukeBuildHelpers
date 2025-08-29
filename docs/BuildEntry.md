# BuildEntry Documentation

This document provides an overview of the fluent API functionalities available for `BuildEntry` through the extension methods provided under the namespace `NukeBuildHelpers.Entry.Extensions`.

All files created on app-specific `OutputDirectory` under all `BuildEntry` will propagate on all `TestEntry` and `PublishEntry` with the same app ID.

## Features

- [AppId](#appid)
- [RunnerOS](#runneros)
- [Execute](#execute)
- [CachePath](#cachepath)
- [CacheInvalidator](#cacheinvalidator)
- [CheckoutFetchDepth](#checkoutfetchdepth)
- [CheckoutFetchTags](#checkoutfetchtags)
- [CheckoutSubmodules](#checkoutsubmodules)
- [Condition](#condition)
- [DisplayName](#displayname)
- [WorkflowId](#workflowid)
- [WorkflowBuilder](#workflowbuilder)
- [Matrix](#matrix)

---

## AppId

⚠️ **REQUIRED**: Sets the app ID(s) of the app. **All entries MUST provide at least one AppId or will throw an error.** Supports both single and multiple application IDs.

### Definitions

```csharp
IBuildEntryDefinition AppId(string appId);
IBuildEntryDefinition AppId(params string[] appIds);
```

### Usage

* Specify single app ID (REQUIRED)

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .AppId("nuke_build_helpers");
    }
    ```

* Specify multiple app IDs

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry MultiAppBuildEntry => _ => _
            .AppId("frontend", "backend", "shared");
    }
    ```

### Error Conditions

If AppId is not specified, you will get these errors:

```
Error: AppIds for [EntryId] is empty
Error: AppIds for [EntryId] contains empty value
```

**Fix**: Always provide at least one valid AppId:

```csharp
// ❌ INVALID - Will throw error
BuildEntry InvalidEntry => _ => _
    .Execute(() => { /* ... */ });

// ✅ VALID - Required AppId provided
BuildEntry ValidEntry => _ => _
    .AppId("my_app")
    .Execute(() => { /* ... */ });
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
            .AppId("my_app")
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
            .AppId("my_app")
            .RunnerOS(context => 
            {
                var contextVersion = context.Apps.First().Value;
                if (contextVersion.IsPullRequest)
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
            .AppId("my_app")
            .Execute(() =>
            {
                DotNetTasks.DotNetBuild(_ => _
                    .SetProjectFile(RootDirectory / "MyProject.csproj"));
            });
    }
    ```

* Running with `IRunContext` - Single App

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .AppId("my_app")
            .Execute(context =>
            {
                var contextVersion = context.Apps.First().Value;
                
                string version = contextVersion.AppVersion.Version.ToString();
                string? releaseNotes = null;
                
                if (contextVersion.BumpVersion != null)
                {
                    version = contextVersion.BumpVersion.Version.ToString();
                    releaseNotes = contextVersion.BumpVersion.ReleaseNotes;
                }
                else if (contextVersion.PullRequestVersion != null)
                {
                    version = contextVersion.PullRequestVersion.Version.ToString();
                }
                
                DotNetTasks.DotNetPack(_ => _
                    .SetProject(RootDirectory / "MyProject.csproj")
                    .SetVersion(version)
                    .SetPackageReleaseNotes(releaseNotes)
                    .SetOutputDirectory(contextVersion.OutputDirectory / "packages"));
            });
    }
    ```

* Running with `IRunContext` - Multiple Apps

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeHelpers
    {
        ...

        BuildEntry MultiAppBuildEntry => _ => _
            .AppId("frontend", "backend")
            .Execute(context =>
            {
                foreach (var appContext in context.Apps.Values)
                {
                    Log.Information("Building: {AppId}", appContext.AppId);
                    
                    string version = appContext.AppVersion.Version.ToString();
                    if (appContext.BumpVersion != null)
                    {
                        version = appContext.BumpVersion.Version.ToString();
                    }
                    
                    DotNetTasks.DotNetBuild(_ => _
                        .SetProjectFile(RootDirectory / appContext.AppId / $"{appContext.AppId}.csproj")
                        .SetConfiguration("Release")
                        .SetOutputDirectory(appContext.OutputDirectory));
                }
            });
    }
    ```

* Context properties and convenience methods

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .AppId("my_app")
            .Execute(context =>
            {
                var contextVersion = context.Apps.First().Value;
                
                // Access app information
                Log.Information("App ID: {AppId}", contextVersion.AppId);
                Log.Information("App Output: {Output}", contextVersion.OutputDirectory);
                
                // Check run type using convenience properties
                if (contextVersion.IsBump)
                {
                    Log.Information("This is a bump/release build");
                    var releaseNotes = contextVersion.BumpVersion.ReleaseNotes;
                }
                else if (contextVersion.IsPullRequest)
                {
                    Log.Information("This is a pull request build");
                    var prNumber = contextVersion.PullRequestVersion.PullRequestNumber;
                }
                else if (contextVersion.IsLocal)
                {
                    Log.Information("This is a local development build");
                }
                else if (contextVersion.IsCommit)
                {
                    Log.Information("This is a commit build");
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
            .AppId("my_app")
            .CachePath(RootDirectory / "directoryToCache")
            .CachePath(RootDirectory / "fileToCache.txt");
    }
    ```

* Resolve at runtime with `IRunContext`

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .AppId("my_app")
            .CachePath(context =>
            {
                var contextVersion = context.Apps.First().Value;
                if (contextVersion.IsPullRequest)
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
            .AppId("my_app")
            .CacheInvalidator("sampleValue");
    }
    ```

* Resolve at runtime with `IRunContext`

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .AppId("my_app")
            .CacheInvalidator(context =>
            {
                var contextVersion = context.Apps.First().Value;
                if (contextVersion.IsBump)
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

## CheckoutFetchDepth

Sets the number of commits to fetch. `0` indicates all history for all branches and tags. Default value is `1`.

### Definitions

```csharp
IBuildEntryDefinition CheckoutFetchDepth(int checkoutFetchDepth);
IBuildEntryDefinition CheckoutFetchDepth(Func<int> checkoutFetchDepth);
IBuildEntryDefinition CheckoutFetchDepth(Func<IRunContext, int> checkoutFetchDepth);
IBuildEntryDefinition CheckoutFetchDepth(Func<Task<int>> checkoutFetchDepth);
IBuildEntryDefinition CheckoutFetchDepth(Func<IRunContext, Task<int>> checkoutFetchDepth);
```

### Usage

* Specify the value directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .AppId("my_app")
            .CheckoutFetchDepth(0);
    }
    ```

* Resolve at runtime with `IRunContext`

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .AppId("my_app")
            .CheckoutFetchDepth(context =>
            {
                var contextVersion = context.Apps.First().Value;
                if (contextVersion.IsBump)
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            });
    }
    ```

---

## CheckoutFetchTags

Sets `true` whether to fetch tags, even if fetch-depth > `0`. Default is `false`.

### Definitions

```csharp
IBuildEntryDefinition CheckoutFetchTags(bool checkoutFetchTags);
IBuildEntryDefinition CheckoutFetchTags(Func<bool> checkoutFetchTags);
IBuildEntryDefinition CheckoutFetchTags(Func<IRunContext, bool> checkoutFetchTags);
IBuildEntryDefinition CheckoutFetchTags(Func<Task<bool>> checkoutFetchTags);
IBuildEntryDefinition CheckoutFetchTags(Func<IRunContext, Task<bool>> checkoutFetchTags);
```

### Usage

* Specify the value directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .AppId("my_app")
            .CheckoutFetchTags(true);
    }
    ```

* Resolve at runtime with `IRunContext`

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .AppId("my_app")
            .CheckoutFetchTags(context =>
            {
                var contextVersion = context.Apps.First().Value;
                if (contextVersion.IsBump)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            });
    }
    ```

---

## CheckoutSubmodule

Sets value on how to checkout submodules. Whether to `SubmoduleCheckoutType.SingleLevel` to checkout submodules or `SubmoduleCheckoutType.Recursive` to checkout submodules of submodules. Default is `SubmoduleCheckoutType.None`.

### Definitions

```csharp
IBuildEntryDefinition CheckoutSubmodule(SubmoduleCheckoutType checkoutSubmodule);
IBuildEntryDefinition CheckoutSubmodule(Func<SubmoduleCheckoutType> checkoutSubmodule);
IBuildEntryDefinition CheckoutSubmodule(Func<IRunContext, SubmoduleCheckoutType> checkoutSubmodule);
IBuildEntryDefinition CheckoutSubmodule(Func<Task<SubmoduleCheckoutType>> checkoutSubmodule);
IBuildEntryDefinition CheckoutSubmodule(Func<IRunContext, Task<SubmoduleCheckoutType>> checkoutSubmodule);
```

### Usage

* Specify the value directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .AppId("my_app")
            .CheckoutSubmodule(SubmoduleCheckoutType.Recursive);
    }
    ```

* Resolve at runtime with `IRunContext`

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .AppId("my_app")
            .CheckoutSubmodule(context =>
            {
                var contextVersion = context.Apps.First().Value;
                if (contextVersion.IsBump)
                {
                    return SubmoduleCheckoutType.Recursive;
                }
                else
                {
                    return SubmoduleCheckoutType.None;
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
            .AppId("my_app")
            .Condition(false);
    }
    ```

* Resolve at runtime with `IRunContext`

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .AppId("my_app")
            .Condition(context =>
            {
                var contextVersion = context.Apps.First().Value;
                return contextVersion.IsBump;
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
            .AppId("my_app")
            .DisplayName("Build Entry Sample");
    }
    ```

---

## WorkflowId

Sets the workflow id of the entry. Modifying the value will need to rebuild the workflow.

### Definitions

```csharp
IBuildEntryDefinition WorkflowId(string workflowId);
```

### Usage

* Specify `string` directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .AppId("my_app")
            .WorkflowId("id_entry_build");
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
            .AppId("my_app")
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

## Matrix

Sets the matrix of the definition to configure on each matrix element.

### Definitions

```csharp
IBuildEntryDefinition Matrix(TMatrix[] matrix, Action<TBuildEntryDefinition, TMatrix> matrixDefinition);
```

### Usage

* Specify matrix directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        BuildEntry SampleBuildEntry => _ => _
            .AppId("my_app")
            .Matrix(new[] { ("Config1", "Release"), ("Config2", "Debug") }, (definition, matrix) => 
            {
                definition.DisplayName("Matrix build " + matrix.Item1 + ", " + matrix.Item2);
                definition.Execute(context =>
                {
                    var contextVersion = context.Apps.First().Value;
                    Log.Information("Building with config: {Config}", matrix.Item1);
                    
                    DotNetTasks.DotNetBuild(_ => _
                        .SetConfiguration(matrix.Item2)
                        .SetOutputDirectory(contextVersion.OutputDirectory / matrix.Item1));
                });
            });
    }
    ```
