using Nuke.Common.IO;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;
using NukeBuildHelpers.Runner.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Entry.Definitions;

internal abstract class EntryDefinition : IEntryDefinition
{
    public required virtual string Id { get; set; }

    Func<IRunContext, Task<string>>? name = null;

    protected abstract string GetDefaultName();

    string IEntryDefinition.Id
    {
        get => Id;
        set => Id = value;
    }

    Func<IRunContext, Task<string>> IEntryDefinition.Name
    {
        get => name ?? (_ => Task.FromResult(GetDefaultName()));
        set => name = value;
    }

    Func<IWorkflowBuilder, Task>? IEntryDefinition.WorkflowBuilder { get; set; }

    Func<IRunContext, Task<string>> IEntryDefinition.CacheInvalidator { get; set; } = _ => Task.FromResult("0");

    Func<IRunContext, Task<AbsolutePath[]>>? IEntryDefinition.CachePaths { get; set; }

    Func<IRunContext, Task>? IEntryDefinition.Execute { get; set; }

    Func<IRunContext, Task<bool>>? IEntryDefinition.Condition { get; set; }

    Func<IRunContext, Task<RunnerOS>>? IEntryDefinition.RunnerOS { get; set; }

    IRunContext? IEntryDefinition.RunContext { get; set; }
}
