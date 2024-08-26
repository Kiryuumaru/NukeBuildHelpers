# WorkflowConfigEntry Documentation

This document provides an overview of the fluent API functionalities available for `WorkflowConfigEntry` through the extension methods provided under the namespace `NukeBuildHelpers.Entry.Extensions`.

This can be implemented by overriding the base class property `BaseNukeBuildHelpers.WorkflowConfig` as shown below:

```csharp
using NukeBuildHelpers.Entry.Extensions;

class Build : BaseNukeBuildHelpers
{
    ...
    
    protected override WorkflowConfigEntry WorkflowConfig => _ => _
        .PreSetupRunnerOS(RunnerOS.Windows2022)
        .PostSetupRunnerOS(RunnerOS.Ubuntu2204);
}
```

## Features

- [Name](#name)
- [PreSetupRunnerOS](#presetuprunneros)
- [PostSetupRunnerOS](#postsetuprunneros)
- [AppendReleaseNotesAssetHashes](#appendreleasenotesassethashes)
- [EnablePrereleaseOnRelease](#enableprereleaseonrelease)

---

## Name

Sets the display name for the workflow to generate. Default is `Nuke CICD Pipeline`.

### Definitions

```csharp
IWorkflowConfigEntryDefinition Name(string name)
IWorkflowConfigEntryDefinition Name(Func<string> name)
IWorkflowConfigEntryDefinition Name(Func<Task<string>> name)
```

### Usage

* Specify directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        WorkflowConfigEntry SampleConfigEntry => _ => _
            .Name("Sample Workflow");
    }
    ```

* Use an asynchronous function

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...

        WorkflowConfigEntry SampleConfigEntry => _ => _
            .Name(async () => await Task.FromResult("Sample Workflow"));
    }
    ```

---

## PreSetupRunnerOS

Sets the pre-setup runner OS for the workflow to generate. Default is `RunnerOS.Ubuntu2204`.

### Definitions

```csharp
IWorkflowConfigEntryDefinition PreSetupRunnerOS(RunnerOS runnerOS)
IWorkflowConfigEntryDefinition PreSetupRunnerOS(Func<RunnerOS> runnerOS)
IWorkflowConfigEntryDefinition PreSetupRunnerOS(Func<Task<RunnerOS>> runnerOS)
```

### Usage

* Specify directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;
    using NukeBuildHelpers.Runner.Models;

    class Build : BaseNukeBuildHelpers
    {
        ...

        WorkflowConfigEntry SampleConfigEntry => _ => _
            .PreSetupRunnerOS(RunnerOS.Ubuntu2204);
    }
    ```

* Use an asynchronous function

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;
    using NukeBuildHelpers.Runner.Models;

    class Build : BaseNukeBuildHelpers
    {
        ...

        WorkflowConfigEntry SampleConfigEntry => _ => _
            .PreSetupRunnerOS(async () => await Task.FromResult(RunnerOS.Ubuntu2204));
    }
    ```

---

## PostSetupRunnerOS

Sets the post-setup runner OS for the workflow to generate. Default is `RunnerOS.Ubuntu2204`.

### Definitions

```csharp
IWorkflowConfigEntryDefinition PostSetupRunnerOS(RunnerOS runnerOS)
IWorkflowConfigEntryDefinition PostSetupRunnerOS(Func<RunnerOS> runnerOS)
IWorkflowConfigEntryDefinition PostSetupRunnerOS(Func<Task<RunnerOS>> runnerOS)
```

### Usage

* Specify directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;
    using NukeBuildHelpers.Runner.Models;

    class Build : BaseNukeBuildHelpers
    {
        ...

        WorkflowConfigEntry SampleConfigEntry => _ => _
            .PostSetupRunnerOS(RunnerOS.Windows2022);
    }
    ```

* Use an asynchronous function

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;
    using NukeBuildHelpers.Runner.Models;

    class Build : BaseNukeBuildHelpers
    {
        ...

        WorkflowConfigEntry SampleConfigEntry => _ => _
            .PostSetupRunnerOS(async () => await Task.FromResult(RunnerOS.Windows2022));
    }
    ```

---

## AppendReleaseNotesAssetHashes

Configures whether to append release notes asset hashes in the workflow. Default is `true`.

### Definitions

```csharp
IWorkflowConfigEntryDefinition AppendReleaseNotesAssetHashes(bool appendReleaseNotesAssetHashes)
IWorkflowConfigEntryDefinition AppendReleaseNotesAssetHashes(Func<bool> appendReleaseNotesAssetHashes)
IWorkflowConfigEntryDefinition AppendReleaseNotesAssetHashes(Func<Task<bool>> appendReleaseNotesAssetHashes)
```

### Usage

* Specify directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...
        
        WorkflowConfigEntry SampleConfigEntry => _ => _
            .AppendReleaseNotesAssetHashes(true);
    }
    ```

* Use an asynchronous function

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...
        
        WorkflowConfigEntry SampleConfigEntry => _ => _
            .AppendReleaseNotesAssetHashes(async () => await Task.FromResult(true));
    }
    ```
    
## EnablePrereleaseOnRelease

Configures whether to enable prerelease on GitHub release if the environment is not on the main branch. Default is `true`.

### Definitions

```csharp
IWorkflowConfigEntryDefinition EnablePrereleaseOnRelease(bool enablePrereleaseOnRelease)
IWorkflowConfigEntryDefinition EnablePrereleaseOnRelease(Func<bool> enablePrereleaseOnRelease)
IWorkflowConfigEntryDefinition EnablePrereleaseOnRelease(Func<Task<bool>> enablePrereleaseOnRelease)
```

### Usage

* Specify directly

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...
        
        WorkflowConfigEntry SampleConfigEntry => _ => _
            .EnablePrereleaseOnRelease(true);
    }
    ```

* Use an asynchronous function

    ```csharp
    using NukeBuildHelpers.Entry.Extensions;

    class Build : BaseNukeBuildHelpers
    {
        ...
        
        WorkflowConfigEntry SampleConfigEntry => _ => _
            .EnablePrereleaseOnRelease(async () => await Task.FromResult(true));
    }
    ```
