using Nuke.Common.IO;
using NukeBuildHelpers.Entry.Enums;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;
using NukeBuildHelpers.Runner.Abstraction;

namespace NukeBuildHelpers.Entry.Definitions;

internal abstract class RunEntryDefinition : IRunEntryDefinition
{
    string? id = null;
    string IRunEntryDefinition.Id
    {
        get => id ?? "_";
        set => id = value;
    }

    List<string> appId = [];
    List<string> IRunEntryDefinition.AppIds
    {
        get => appId;
        set => appId = value;
    }

    Func<IWorkflowBuilder, Task<string>>? displayName = null;
    Func<IWorkflowBuilder, Task<string>> IRunEntryDefinition.DisplayName
    {
        get => displayName ?? (_ => Task.FromResult(GetDefaultName()));
        set => displayName = value;
    }

    Func<IRunContext, Task<bool>>? condition = null;
    Func<IRunContext, Task<bool>> IRunEntryDefinition.Condition
    {
        get => condition ?? (_ => Task.FromResult(true));
        set => condition = value;
    }

    Func<IRunContext, Task<RunnerOS>>? runnerOS;
    Func<IRunContext, Task<RunnerOS>>? IRunEntryDefinition.RunnerOS
    {
        get => runnerOS;
        set => runnerOS = value;
    }

    List<Func<IRunContext, Task<AbsolutePath[]>>>? cachePath;
    List<Func<IRunContext, Task<AbsolutePath[]>>> IRunEntryDefinition.CachePath
    {
        get => cachePath ?? [];
        set => cachePath = value;
    }

    Func<IRunContext, Task<string>>? cacheInvalidator;
    Func<IRunContext, Task<string>> IRunEntryDefinition.CacheInvalidator
    {
        get => cacheInvalidator ?? (_ => Task.FromResult("0"));
        set => cacheInvalidator = value;
    }

    Func<IRunContext, Task<int>>? checkoutFetchDepth;
    Func<IRunContext, Task<int>> IRunEntryDefinition.CheckoutFetchDepth
    {
        get => checkoutFetchDepth ?? (_ => Task.FromResult(1));
        set => checkoutFetchDepth = value;
    }

    Func<IRunContext, Task<bool>>? checkoutFetchTags;
    Func<IRunContext, Task<bool>> IRunEntryDefinition.CheckoutFetchTags
    {
        get => checkoutFetchTags ?? (_ => Task.FromResult(false));
        set => checkoutFetchTags = value;
    }

    Func<IRunContext, Task<SubmoduleCheckoutType>>? checkoutSubmodules;
    Func<IRunContext, Task<SubmoduleCheckoutType>> IRunEntryDefinition.CheckoutSubmodules
    {
        get => checkoutSubmodules ?? (_ => Task.FromResult(SubmoduleCheckoutType.None));
        set => checkoutSubmodules = value;
    }

    List<Func<IRunContext, Task>>? execute;
    List<Func<IRunContext, Task>> IRunEntryDefinition.Execute
    {
        get => execute ?? [];
        set => execute = value;
    }

    List<Func<IWorkflowBuilder, Task>>? workflowBuilder;
    List<Func<IWorkflowBuilder, Task>> IRunEntryDefinition.WorkflowBuilder
    {
        get => workflowBuilder ?? [];
        set => workflowBuilder = value;
    }

    List<Func<IRunEntryDefinition, Task<IRunEntryDefinition[]>>>? matrix;
    List<Func<IRunEntryDefinition, Task<IRunEntryDefinition[]>>> IRunEntryDefinition.Matrix
    {
        get => matrix ?? [];
        set => matrix = value;
    }

    IRunContext? IRunEntryDefinition.RunContext { get; set; }

    protected abstract string GetDefaultName();

    protected abstract IRunEntryDefinition Clone();

    IRunEntryDefinition IRunEntryDefinition.Clone() => Clone();

    internal virtual void FillClone(IRunEntryDefinition definition)
    {
        if (id != null) definition.Id = id;
        if (appId != null) definition.AppIds = [.. appId];
        if (displayName != null) definition.DisplayName = displayName;
        if (condition != null) definition.Condition = condition;
        if (runnerOS != null) definition.RunnerOS = runnerOS;
        if (cachePath != null) definition.CachePath = new List<Func<IRunContext, Task<AbsolutePath[]>>>(cachePath);
        if (cacheInvalidator != null) definition.CacheInvalidator = cacheInvalidator;
        if (checkoutFetchDepth != null) definition.CheckoutFetchDepth = checkoutFetchDepth;
        if (checkoutFetchTags != null) definition.CheckoutFetchTags = checkoutFetchTags;
        if (checkoutSubmodules != null) definition.CheckoutSubmodules = checkoutSubmodules;
        if (execute != null) definition.Execute = new List<Func<IRunContext, Task>>(execute);
        if (workflowBuilder != null) definition.WorkflowBuilder = new List<Func<IWorkflowBuilder, Task>>(workflowBuilder);
        if (matrix != null) definition.Matrix = new List<Func<IRunEntryDefinition, Task<IRunEntryDefinition[]>>>(matrix);

        definition.RunContext = ((IRunEntryDefinition)this).RunContext;
    }
}
