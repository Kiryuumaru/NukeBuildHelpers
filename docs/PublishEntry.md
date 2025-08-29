# PublishEntry Documentation

This document provides an overview of the fluent API functionalities available for `PublishEntry` through the extension methods provided under the namespace `NukeBuildHelpers.Entry.Extensions`.

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
- [Release Assets](#release-assets)
- [Matrix](#matrix)

---

## AppId

⚠️ **REQUIRED**: Sets the app ID(s) of the app. **All entries MUST provide at least one AppId or will throw an error.** Supports both single and multiple application IDs.

### Definitions

```csharp
IPublishEntryDefinition AppId(string appId);
IPublishEntryDefinition AppId(params string[] appIds);
```

### Usage

* Specify single app ID (REQUIRED)

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        PublishEntry SamplePublishEntry => _ => _
            .AppId("nuke_build_helpers");
    }
    ```

* Specify multiple app IDs

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        PublishEntry MultiAppPublishEntry => _ => _
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
PublishEntry InvalidEntry => _ => _
    .Execute(() => { /* ... */ });

// ✅ VALID - Required AppId provided
PublishEntry ValidEntry => _ => _
    .AppId("my_app")
    .Execute(() => { /* ... */ });
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

        PublishEntry SamplePublishEntry => _ => _
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
            .AppId("my_app")
            .Execute(async () =>
            {
                DotNetTasks.DotNetNuGetPush(_ => _
                    .SetSource("https://api.nuget.org/v3/index.json")
                    .SetApiKey(NuGetAuthToken)
                    .SetTargetPath(RootDirectory / "packages" / "**"));
            });
    }
    ```

* Running with `IRunContext` - Single App

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        PublishEntry SamplePublishEntry => _ => _
            .AppId("my_app")
            .Execute(async context =>
            {
                var contextVersion = context.Apps.First().Value;
                
                if (contextVersion.IsBump)
                {
                    DotNetTasks.DotNetNuGetPush(_ => _
                        .SetSource("https://api.nuget.org/v3/index.json")
                        .SetApiKey(NuGetAuthToken)
                        .SetTargetPath(contextVersion.OutputDirectory / "packages" / "**"));
                }
                
                // Add release assets using the new static method
                await AddReleaseAsset(contextVersion.OutputDirectory / "packages");
                await AddReleaseAsset(contextVersion.OutputDirectory / "documentation.zip");
            });
    }
    ```

* Running with `IRunContext` - Multiple Apps

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        PublishEntry MultiAppPublishEntry => _ => _
            .AppId("frontend", "backend")
            .Execute(async context =>
            {
                foreach (var appContext in context.Apps.Values)
                {
                    Log.Information("Publishing: {AppId}", appContext.AppId);
                    
                    if (appContext.IsBump)
                    {
                        // Publish each app's packages
                        DotNetTasks.DotNetNuGetPush(_ => _
                            .SetSource("https://api.nuget.org/v3/index.json")
                            .SetApiKey(NuGetAuthToken)
                            .SetTargetPath(appContext.OutputDirectory / "packages" / "**"));
                        
                        // Add each app's assets to release
                        await AddReleaseAsset(appContext.OutputDirectory / "packages", $"{appContext.AppId}-packages");
                    }
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

        PublishEntry SamplePublishEntry => _ => _
            .AppId("my_app")
            .Execute(async context =>
            {
                var contextVersion = context.Apps.First().Value;
                
                // Check run type using convenience properties
                if (contextVersion.IsBump)
                {
                    Log.Information("Publishing release: {Version}", contextVersion.BumpVersion.Version);
                    var releaseNotes = contextVersion.BumpVersion.ReleaseNotes;
                    
                    // Publish to NuGet
                    DotNetTasks.DotNetNuGetPush(_ => _
                        .SetSource("https://api.nuget.org/v3/index.json")
                        .SetApiKey(NuGetAuthToken)
                        .SetTargetPath(contextVersion.OutputDirectory / "packages" / "**"));
                }
                else if (contextVersion.IsPullRequest)
                {
                    Log.Information("Skipping publish for PR #{Number}", 
                        contextVersion.PullRequestVersion.PullRequestNumber);
                }
                
                // Always add assets for release
                await AddReleaseAsset(contextVersion.OutputDirectory / "build-artifacts");
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

        PublishEntry SamplePublishEntry => _ => _
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

        PublishEntry SamplePublishEntry => _ => _
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
IPublishEntryDefinition CheckoutFetchDepth(int checkoutFetchDepth);
IPublishEntryDefinition CheckoutFetchDepth(Func<int> checkoutFetchDepth);
IPublishEntryDefinition CheckoutFetchDepth(Func<IRunContext, int> checkoutFetchDepth);
IPublishEntryDefinition CheckoutFetchDepth(Func<Task<int>> checkoutFetchDepth);
IPublishEntryDefinition CheckoutFetchDepth(Func<IRunContext, Task<int>> checkoutFetchDepth);
```

### Usage

* Specify the value directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        PublishEntry SamplePublishEntry => _ => _
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

        PublishEntry SamplePublishEntry => _ => _
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
IPublishEntryDefinition CheckoutFetchTags(bool checkoutFetchTags);
IPublishEntryDefinition CheckoutFetchTags(Func<bool> checkoutFetchTags);
IPublishEntryDefinition CheckoutFetchTags(Func<IRunContext, bool> checkoutFetchTags);
IPublishEntryDefinition CheckoutFetchTags(Func<Task<bool>> checkoutFetchTags);
IPublishEntryDefinition CheckoutFetchTags(Func<IRunContext, Task<bool>> checkoutFetchTags);
```

### Usage

* Specify the value directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        PublishEntry SamplePublishEntry => _ => _
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

        PublishEntry SamplePublishEntry => _ => _
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
IPublishEntryDefinition CheckoutSubmodule(SubmoduleCheckoutType checkoutSubmodule);
IPublishEntryDefinition CheckoutSubmodule(Func<SubmoduleCheckoutType> checkoutSubmodule);
IPublishEntryDefinition CheckoutSubmodule(Func<IRunContext, SubmoduleCheckoutType> checkoutSubmodule);
IPublishEntryDefinition CheckoutSubmodule(Func<Task<SubmoduleCheckoutType>> checkoutSubmodule);
IPublishEntryDefinition CheckoutSubmodule(Func<IRunContext, Task<SubmoduleCheckoutType>> checkoutSubmodule);
```

### Usage

* Specify the value directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        PublishEntry SamplePublishEntry => _ => _
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

        PublishEntry SamplePublishEntry => _ => _
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

        PublishEntry SamplePublishEntry => _ => _
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
            .AppId("my_app")
            .DisplayName("Publish Entry Sample");
    }
    ```

---

## WorkflowId

Sets the workflow id of the entry. Modifying the value will need to rebuild the workflow.

### Definitions

```csharp
IPublishEntryDefinition WorkflowId(string workflowId);
```

### Usage

* Specify `string` directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        PublishEntry SamplePublishEntry => _ => _
            .AppId("my_app")
            .WorkflowId("id_entry_publish");
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

## Release Assets

**New in V9**: Release assets are now managed using a static method instead of extension methods.

### Method

```csharp
/// <summary>
/// Adds a file or directory path to the collection of individual release assets.
/// If the path is a directory, it will be zipped before being uploaded to the release.
/// </summary>
/// <param name="path">The absolute path to the file or directory to include as a release asset.</param>
/// <param name="customFilename">The custom filename of the asset for release</param>
public static async Task AddReleaseAsset(AbsolutePath path, string? customFilename = null)
```

### Usage

* Add a file as release asset

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        PublishEntry SamplePublishEntry => _ => _
            .AppId("my_app")
            .Execute(async context =>
            {
                var contextVersion = context.Apps.First().Value;
                
                // Add a single file
                await AddReleaseAsset(contextVersion.OutputDirectory / "package.zip");
                
                // Add with custom filename
                await AddReleaseAsset(contextVersion.OutputDirectory / "docs.pdf", "Documentation.pdf");
            });
    }
    ```

* Add a directory as release asset (automatically zipped)

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        PublishEntry SamplePublishEntry => _ => _
            .AppId("my_app")
            .Execute(async context =>
            {
                var contextVersion = context.Apps.First().Value;
                
                // Add entire directory (will be zipped automatically)
                await AddReleaseAsset(contextVersion.OutputDirectory / "build-artifacts");
                
                // Add directory with custom name
                await AddReleaseAsset(contextVersion.OutputDirectory / "packages", "release-packages");
            });
    }
    ```

* Multiple apps with individual assets

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        PublishEntry MultiAppPublishEntry => _ => _
            .AppId("frontend", "backend")
            .Execute(async context =>
            {
                foreach (var appContext in context.Apps.Values)
                {
                    if (appContext.IsBump)
                    {
                        // Add each app's assets with app-specific naming
                        await AddReleaseAsset(appContext.OutputDirectory / "dist", $"{appContext.AppId}-dist");
                        await AddReleaseAsset(appContext.OutputDirectory / "packages", $"{appContext.AppId}-packages");
                    }
                }
            });
    }
    ```

### Migration from V8

```csharp
// V8 (OLD) - Extension methods (REMOVED)
PublishEntry OldPublish => _ => _
    .ReleaseAsset(OutputDirectory / "assets")
    .ReleaseCommonAsset(OutputDirectory / "common")
    .Execute(context => { /* ... */ });

// V9 (NEW) - Static method in Execute
PublishEntry NewPublish => _ => _
    .AppId("my_app")
    .Execute(async context =>
    {
        var contextVersion = context.Apps.First().Value;
        
        // Use static method instead
        await AddReleaseAsset(contextVersion.OutputDirectory / "assets");
        await AddReleaseAsset(contextVersion.OutputDirectory / "common");
        
        /* ... */
    });
```

---

## Matrix

Sets the matrix of the definition to configure on each matrix element.

### Definitions

```csharp
IPublishEntryDefinition Matrix(TMatrix[] matrix, Action<TPublishEntryDefinition, TMatrix> matrixDefinition);
```

### Usage

* Specify matrix directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        PublishEntry SamplePublishEntry => _ => _
            .AppId("my_app")
            .Matrix(new[] { ("NuGet", "https://api.nuget.org/v3/index.json"), ("GitHub", "https://nuget.pkg.github.com/user/index.json") }, (definition, matrix) => 
            {
                definition.DisplayName("Publish to " + matrix.Item1);
                definition.Execute(async context =>
                {
                    var contextVersion = context.Apps.First().Value;
                    Log.Information("Publishing to: {Registry}", matrix.Item1);
                    
                    if (contextVersion.IsBump)
                    {
                        DotNetTasks.DotNetNuGetPush(_ => _
                            .SetSource(matrix.Item2)
                            .SetApiKey(GetApiKeyForRegistry(matrix.Item1))
                            .SetTargetPath(contextVersion.OutputDirectory / "packages" / "**"));
                    }
                });
            })
