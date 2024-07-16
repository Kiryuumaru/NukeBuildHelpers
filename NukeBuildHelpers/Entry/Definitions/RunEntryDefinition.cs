using Nuke.Common.IO;
using NukeBuildHelpers.Entry.Enums;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;
using NukeBuildHelpers.Runner.Abstraction;

namespace NukeBuildHelpers.Entry.Definitions;

internal abstract class RunEntryDefinition : IRunEntryDefinition
{
    public required virtual string Id { get; set; }

    Func<IWorkflowBuilder, Task<string>>? name = null;

    Func<IRunContext, Task<bool>>? condition = null;

    protected abstract string GetDefaultName();

    protected abstract IRunEntryDefinition Clone();

    IRunEntryDefinition IRunEntryDefinition.Clone() => Clone();

    internal virtual void FillClone(IRunEntryDefinition definition)
    {
        definition.Id = ((IRunEntryDefinition)this).Id;
        definition.DisplayName = ((IRunEntryDefinition)this).DisplayName;
        definition.Condition = ((IRunEntryDefinition)this).Condition;
        definition.RunnerOS = ((IRunEntryDefinition)this).RunnerOS;
        definition.CachePath = new List<Func<IRunContext, Task<AbsolutePath[]>>>(((IRunEntryDefinition)this).CachePath);
        definition.CacheInvalidator = ((IRunEntryDefinition)this).CacheInvalidator;
        definition.CheckoutFetchDepth = ((IRunEntryDefinition)this).CheckoutFetchDepth;
        definition.CheckoutFetchTags = ((IRunEntryDefinition)this).CheckoutFetchTags;
        definition.CheckoutSubmodules = ((IRunEntryDefinition)this).CheckoutSubmodules;
        definition.Execute = new List<Func<IRunContext, Task>>(((IRunEntryDefinition)this).Execute);
        definition.WorkflowBuilder = new List<Func<IWorkflowBuilder, Task>>(((IRunEntryDefinition)this).WorkflowBuilder);
        definition.RunContext = ((IRunEntryDefinition)this).RunContext;
    }

    string IRunEntryDefinition.Id
    {
        get => Id;
        set => Id = value;
    }

    Func<IWorkflowBuilder, Task<string>> IRunEntryDefinition.DisplayName
    {
        get => name ?? (_ => Task.FromResult(GetDefaultName()));
        set => name = value;
    }

    Func<IRunContext, Task<bool>> IRunEntryDefinition.Condition
    {
        get => condition ?? (runContext => Task.FromResult(true));
        set => condition = value;
    }

    Func<IRunContext, Task<RunnerOS>>? IRunEntryDefinition.RunnerOS { get; set; }

    List<Func<IRunContext, Task<AbsolutePath[]>>> IRunEntryDefinition.CachePath { get; set; } = [];

    Func<IRunContext, Task<string>> IRunEntryDefinition.CacheInvalidator { get; set; } = _ => Task.FromResult("0");

    Func<IRunContext, Task<int>> IRunEntryDefinition.CheckoutFetchDepth { get; set; } = _ => Task.FromResult(1);

    Func<IRunContext, Task<bool>> IRunEntryDefinition.CheckoutFetchTags { get; set; } = _ => Task.FromResult(false);

    Func<IRunContext, Task<SubmoduleCheckoutType>> IRunEntryDefinition.CheckoutSubmodules { get; set; } = _ => Task.FromResult(SubmoduleCheckoutType.None);

    List<Func<IRunContext, Task>> IRunEntryDefinition.Execute { get; set; } = [];

    List<Func<IWorkflowBuilder, Task>> IRunEntryDefinition.WorkflowBuilder { get; set; } = [];

    List<Func<IRunEntryDefinition, Task<IRunEntryDefinition[]>>> IRunEntryDefinition.Matrix { get; set; } = [];

    IRunContext? IRunEntryDefinition.RunContext { get; set; }
}
