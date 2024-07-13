using Nuke.Common.IO;
using NukeBuildHelpers.Entry.Enums;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;
using NukeBuildHelpers.Runner.Abstraction;

namespace NukeBuildHelpers.Entry.Definitions;

internal abstract class EntryDefinition : IEntryDefinition
{
    public required virtual string Id { get; set; }

    Func<IWorkflowBuilder, Task<string>>? name = null;

    Func<IRunContext, Task<bool>>? condition = null;

    protected abstract string GetDefaultName();

    protected abstract IEntryDefinition Clone();

    IEntryDefinition IEntryDefinition.Clone() => Clone();

    internal virtual void FillClone(IEntryDefinition definition)
    {
        definition.Id = ((IEntryDefinition)this).Id;
        definition.DisplayName = ((IEntryDefinition)this).DisplayName;
        definition.Condition = ((IEntryDefinition)this).Condition;
        definition.RunnerOS = ((IEntryDefinition)this).RunnerOS;
        definition.CachePath = ((IEntryDefinition)this).CachePath;
        definition.CacheInvalidator = ((IEntryDefinition)this).CacheInvalidator;
        definition.CheckoutFetchDepth = ((IEntryDefinition)this).CheckoutFetchDepth;
        definition.CheckoutFetchTags = ((IEntryDefinition)this).CheckoutFetchTags;
        definition.CheckoutSubmodules = ((IEntryDefinition)this).CheckoutSubmodules;
        definition.Execute = ((IEntryDefinition)this).Execute;
        definition.WorkflowBuilder = ((IEntryDefinition)this).WorkflowBuilder;
        definition.RunContext = ((IEntryDefinition)this).RunContext;
    }

    string IEntryDefinition.Id
    {
        get => Id;
        set => Id = value;
    }

    Func<IWorkflowBuilder, Task<string>> IEntryDefinition.DisplayName
    {
        get => name ?? (_ => Task.FromResult(GetDefaultName()));
        set => name = value;
    }

    Func<IRunContext, Task<bool>> IEntryDefinition.Condition
    {
        get => condition ?? (runContext => Task.FromResult(true));
        set => condition = value;
    }

    Func<IRunContext, Task<RunnerOS>>? IEntryDefinition.RunnerOS { get; set; }

    List<Func<IRunContext, Task<AbsolutePath[]>>> IEntryDefinition.CachePath { get; set; } = [];

    Func<IRunContext, Task<string>> IEntryDefinition.CacheInvalidator { get; set; } = _ => Task.FromResult("0");

    Func<IRunContext, Task<int>> IEntryDefinition.CheckoutFetchDepth { get; set; } = _ => Task.FromResult(1);

    Func<IRunContext, Task<bool>> IEntryDefinition.CheckoutFetchTags { get; set; } = _ => Task.FromResult(false);

    Func<IRunContext, Task<SubmoduleCheckoutType>> IEntryDefinition.CheckoutSubmodules { get; set; } = _ => Task.FromResult(SubmoduleCheckoutType.None);

    List<Func<IRunContext, Task>> IEntryDefinition.Execute { get; set; } = [];

    List<Func<IWorkflowBuilder, Task>> IEntryDefinition.WorkflowBuilder { get; set; } = [];

    List<Func<IEntryDefinition, Task<IEntryDefinition[]>>> IEntryDefinition.Matrix { get; set; } = [];

    IRunContext? IEntryDefinition.RunContext { get; set; }
}
