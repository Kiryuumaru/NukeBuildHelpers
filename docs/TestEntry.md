# TestEntry Documentation

This document provides an overview of the fluent API functionalities available for `TestEntry` through the extension methods provided under the namespace `NukeBuildHelpers.Entry.Extensions`.

If test entry run errors, the `BuildEntry` and `PublishEntry` configured will not run.

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
- [WorkflowBuilder](#workflowbuilder)
- [Matrix](#matrix)

---

## AppId

Sets the app IDs of the test to target. It can contain multiple app IDs.

### Definitions

```csharp
ITestEntryDefinition AppId(params string[] appIds);
```

### Usage

* Specify directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        TestEntry SampleTestEntry => _ => _
            .AppId("nuke_build_helpers");
    }
    ```

---

## RunnerOS

Sets the runner OS for the execution. Can choose pre-listed OS under the namespace `NukeBuildHelpers.Runner.Models` or specify custom by overriding the abstract class `RunnerOS`.

### Definitions

```csharp
ITestEntryDefinition RunnerOS(RunnerOS runnerOS);
ITestEntryDefinition RunnerOS(Func<RunnerOS> runnerOS);
ITestEntryDefinition RunnerOS(Func<IRunContext, RunnerOS> runnerOS);
ITestEntryDefinition RunnerOS(Func<Task<RunnerOS>> runnerOS);
ITestEntryDefinition RunnerOS(Func<IRunContext, Task<RunnerOS>> runnerOS);
```

### Usage

* Specify to use `RunnerOS.Ubuntu2204` directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;
    using NukeBuildHelpers.Runner.Models;

    class Build : BaseNukeBuildHelpers
    {
        ...

        TestEntry SampleTestEntry => _ => _
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

        TestEntry SampleTestEntry => _ => _
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
ITestEntryDefinition Execute(Func<T> action);
ITestEntryDefinition Execute(Func<Task> action);
ITestEntryDefinition Execute(Func<Task<T>> action);
ITestEntryDefinition Execute(Action<IRunContext> action);
ITestEntryDefinition Execute(Func<IRunContext, Task> action);
ITestEntryDefinition Execute(Func<IRunContext, Task<T>> action);
```

### Usage

* Running any plain execution

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        TestEntry SampleTestEntry => _ => _
            .Execute(() =>
            {
                DotNetTasks.DotNetClean(_ => _
                    .SetProject(RootDirectory / "NukeBuildHelpers.UnitTest" / "NukeBuildHelpers.UnitTest.csproj"));
                DotNetTasks.DotNetTest(_ => _
                    .SetProjectFile(RootDirectory / "NukeBuildHelpers.UnitTest" / "NukeBuildHelpers.UnitTest.csproj"));
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

        TestEntry SampleTestEntry => _ => _
            .Execute(context =>
            {
                if (context.RunType == RunType.PullRequest)
                {
                    DotNetTasks.DotNetClean(_ => _
                        .SetProject(RootDirectory / "NukeBuildHelpers.UnitTest" / "NukeBuildHelpers.UnitTest.csproj"));
                    DotNetTasks.DotNetTest(_ => _
                        .SetProjectFile(RootDirectory / "NukeBuildHelpers.UnitTest" / "NukeBuildHelpers.UnitTest.csproj"));
                }
            });
    }
    ```

---

## CachePath

Sets the paths to cache using `AbsolutePath`.

### Definitions

```csharp
ITestEntryDefinition CachePath(params AbsolutePath[] cachePath);
ITestEntryDefinition CachePath(Func<AbsolutePath[]> cachePaths);
ITestEntryDefinition CachePath(Func<IRunContext, AbsolutePath[]> cachePaths);
ITestEntryDefinition CachePath(Func<Task<AbsolutePath[]>> cachePaths);
ITestEntryDefinition CachePath(Func<IRunContext, Task<AbsolutePath[]>> cachePaths);
```

### Usage

* Specify `AbsolutePath` directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        TestEntry SampleTestEntry => _ => _
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

        TestEntry SampleTestEntry => _ => _
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
ITestEntryDefinition CacheInvalidator(string cacheInvalidator);
ITestEntryDefinition CacheInvalidator(Func<string> cacheInvalidator);
ITestEntryDefinition CacheInvalidator(Func<IRunContext, string> cacheInvalidator);
ITestEntryDefinition CacheInvalidator(Func<Task<string>> cacheInvalidator);
ITestEntryDefinition CacheInvalidator(Func<IRunContext, Task<string>> cacheInvalidator);
```

### Usage

* Specify the value directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        TestEntry SampleTestEntry => _ => _
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

        TestEntry SampleTestEntry => _ => _
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

## CheckoutFetchDepth

Sets the number of commits to fetch. `0` indicates all history for all branches and tags. Default value is `1`.

### Definitions

```csharp
ITestEntryDefinition CheckoutFetchDepth(int checkoutFetchDepth);
ITestEntryDefinition CheckoutFetchDepth(Func<int> checkoutFetchDepth);
ITestEntryDefinition CheckoutFetchDepth(Func<IRunContext, int> checkoutFetchDepth);
ITestEntryDefinition CheckoutFetchDepth(Func<Task<int>> checkoutFetchDepth);
ITestEntryDefinition CheckoutFetchDepth(Func<IRunContext, Task<int>> checkoutFetchDepth);
```

### Usage

* Specify the value directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        TestEntry SampleTestEntry => _ => _
            .CheckoutFetchDepth(0);
    }
    ```

* Resolve at runtime with `IRunContext`

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;
    using NukeBuildHelpers.Common.Enums;

    class Build : BaseNukeBuildHelpers
    {
        ...

        TestEntry SampleTestEntry => _ => _
            .CheckoutFetchDepth(context =>
            {
                if (context.RunType == RunType.Bump)
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
ITestEntryDefinition CheckoutFetchTags(bool checkoutFetchTags);
ITestEntryDefinition CheckoutFetchTags(Func<bool> checkoutFetchTags);
ITestEntryDefinition CheckoutFetchTags(Func<IRunContext, bool> checkoutFetchTags);
ITestEntryDefinition CheckoutFetchTags(Func<Task<bool>> checkoutFetchTags);
ITestEntryDefinition CheckoutFetchTags(Func<IRunContext, Task<bool>> checkoutFetchTags);
```

### Usage

* Specify the value directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        TestEntry SampleTestEntry => _ => _
            .CheckoutFetchTags(true);
    }
    ```

* Resolve at runtime with `IRunContext`

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;
    using NukeBuildHelpers.Common.Enums;

    class Build : BaseNukeBuildHelpers
    {
        ...

        TestEntry SampleTestEntry => _ => _
            .CheckoutFetchDepth(context =>
            {
                if (context.RunType == RunType.Bump)
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
ITestEntryDefinition CheckoutSubmodule(SubmoduleCheckoutType checkoutSubmodule);
ITestEntryDefinition CheckoutSubmodule(Func<SubmoduleCheckoutType> checkoutSubmodule);
ITestEntryDefinition CheckoutSubmodule(Func<IRunContext, SubmoduleCheckoutType> checkoutSubmodule);
ITestEntryDefinition CheckoutSubmodule(Func<Task<SubmoduleCheckoutType>> checkoutSubmodule);
ITestEntryDefinition CheckoutSubmodule(Func<IRunContext, Task<SubmoduleCheckoutType>> checkoutSubmodule);
```

### Usage

* Specify the value directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        TestEntry SampleTestEntry => _ => _
            .CheckoutSubmodule(SubmoduleCheckoutType.Recursive);
    }
    ```

* Resolve at runtime with `IRunContext`

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;
    using NukeBuildHelpers.Common.Enums;

    class Build : BaseNukeBuildHelpers
    {
        ...

        TestEntry SampleTestEntry => _ => _
            .CheckoutSubmodule(context =>
            {
                if (context.RunType == RunType.Bump)
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
ITestEntryDefinition Condition(bool condition);
ITestEntryDefinition Condition(Func<bool> condition);
ITestEntryDefinition Condition(Func<IRunContext, bool> condition);
ITestEntryDefinition Condition(Func<Task<bool>> condition);
ITestEntryDefinition Condition(Func<IRunContext, Task<bool>> condition);
```

### Usage

* Specify `bool` directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        TestEntry SampleTestEntry => _ => _
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

        TestEntry SampleTestEntry => _ => _
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
ITestEntryDefinition DisplayName(string displayName);
ITestEntryDefinition DisplayName(Func<string> displayName);
ITestEntryDefinition DisplayName(Func<Task<string>> displayName);
```

### Usage

* Specify `string` directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        TestEntry SampleTestEntry => _ => _
            .DisplayName("Test Entry Sample");
    }
    ```

---

## WorkflowBuilder

Sets custom workflow tasks or steps on any supported pipelines. Modifying the value will need to rebuild the workflow.

### Definitions

```csharp
ITestEntryDefinition WorkflowBuilder(Action<IWorkflowBuilder> workflowBuilder);
ITestEntryDefinition WorkflowBuilder(Func<IWorkflowBuilder, Task> workflowBuilder);
ITestEntryDefinition WorkflowBuilder(Func<IWorkflowBuilder, Task<T>> workflowBuilder);
```

### Usage

* Specify directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        TestEntry SampleTestEntry => _ => _
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
ITestEntryDefinition Matrix(TMatrix[] matrix, Action<TEntryDefinition, TMatrix> matrixDefinition);
```

### Usage

* Specify `string` directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        TestEntry SampleTestEntry => _ => _
            .Matrix(new[] { ("Mat1", 3), ("Mat2", 4) }, (definition, matrix) => 
            {
                definition.DisplayName("Matrix test " + matrix.Item1 + ", " + matrix.Item2.ToString());
                definition.Execute(() =>
                {
                    Log.Information("I am hereeee: {s}", matrix.Item1 + ", " + matrix.Item2.ToString());
                });
            })
    }
    ```
