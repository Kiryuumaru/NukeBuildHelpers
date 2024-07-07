using Nuke.Common.IO;
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

    Func<IRunContext, Task<string>> IEntryDefinition.CacheInvalidator { get; set; } = _ => Task.FromResult("0");

    Func<IRunContext, Task<RunnerOS>>? IEntryDefinition.RunnerOS { get; set; }

    List<Func<IRunContext, Task<AbsolutePath[]>>> IEntryDefinition.CachePath { get; set; } = [];

    List<Func<IRunContext, Task>> IEntryDefinition.Execute { get; set; } = [];

    List<Func<IWorkflowBuilder, Task>> IEntryDefinition.WorkflowBuilder { get; set; } = [];

    IRunContext? IEntryDefinition.RunContext { get; set; }
}
