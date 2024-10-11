using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Runner.Abstraction;

namespace NukeBuildHelpers.Entry.Definitions;

internal class WorkflowConfigEntryDefinition : IWorkflowConfigEntryDefinition
{
    Func<Task<string>>? name = null;
    Func<Task<string>> IWorkflowConfigEntryDefinition.Name
    {
        get => name ?? (() => Task.FromResult("Nuke CICD Pipeline"));
        set => name = value;
    }

    Func<Task<RunnerOS>>? preSetupRunnerOS = null;
    Func<Task<RunnerOS>> IWorkflowConfigEntryDefinition.PreSetupRunnerOS
    {
        get => preSetupRunnerOS ?? (() => Task.FromResult(RunnerOS.Ubuntu2204));
        set => preSetupRunnerOS = value;
    }

    Func<Task<RunnerOS>>? postSetupRunnerOS = null;
    Func<Task<RunnerOS>> IWorkflowConfigEntryDefinition.PostSetupRunnerOS
    {
        get => postSetupRunnerOS ?? (() => Task.FromResult(RunnerOS.Ubuntu2204));
        set => postSetupRunnerOS = value;
    }

    Func<Task<bool>>? appendReleaseNotesAssetHashes = null;
    Func<Task<bool>> IWorkflowConfigEntryDefinition.AppendReleaseNotesAssetHashes
    {
        get => appendReleaseNotesAssetHashes ?? (() => Task.FromResult(true));
        set => appendReleaseNotesAssetHashes = value;
    }

    Func<Task<bool>>? enablePrereleaseOnReleases = null;
    Func<Task<bool>> IWorkflowConfigEntryDefinition.EnablePrereleaseOnRelease
    {
        get => enablePrereleaseOnReleases ?? (() => Task.FromResult(true));
        set => enablePrereleaseOnReleases = value;
    }

    Func<Task<long>>? startingBuildId = null;
    Func<Task<long>> IWorkflowConfigEntryDefinition.StartingBuildId
    {
        get => startingBuildId ?? (() => Task.FromResult(1L));
        set => startingBuildId = value;
    }

    Func<Task<bool>>? useJsonFileVersioning = null;
    Func<Task<bool>> IWorkflowConfigEntryDefinition.UseJsonFileVersioning
    {
        get => useJsonFileVersioning ?? (() => Task.FromResult(false));
        set => useJsonFileVersioning = value;
    }

    IWorkflowConfigEntryDefinition IWorkflowConfigEntryDefinition.Clone()
    {
        var definition = new WorkflowConfigEntryDefinition();
        if (name != null) ((IWorkflowConfigEntryDefinition)this).Name = name;
        if (preSetupRunnerOS != null) ((IWorkflowConfigEntryDefinition)this).PreSetupRunnerOS = preSetupRunnerOS;
        if (postSetupRunnerOS != null) ((IWorkflowConfigEntryDefinition)this).PostSetupRunnerOS = postSetupRunnerOS;
        if (appendReleaseNotesAssetHashes != null) ((IWorkflowConfigEntryDefinition)this).AppendReleaseNotesAssetHashes = appendReleaseNotesAssetHashes;
        if (enablePrereleaseOnReleases != null) ((IWorkflowConfigEntryDefinition)this).EnablePrereleaseOnRelease = enablePrereleaseOnReleases;
        if (startingBuildId != null) ((IWorkflowConfigEntryDefinition)this).StartingBuildId = startingBuildId;
        if (useJsonFileVersioning != null) ((IWorkflowConfigEntryDefinition)this).UseJsonFileVersioning = useJsonFileVersioning;
        return definition;
    }
}
