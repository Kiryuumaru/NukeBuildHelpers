using Nuke.Common;
using Nuke.Common.IO;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;
using NukeBuildHelpers.Runner.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Entry.Interfaces;

public interface IEntryDefinition
{
    internal string Id { get; set; }

    internal Func<IWorkflowBuilder, Task>? WorkflowBuilder { get; set; }

    internal Func<IRunContext, Task<string>> Name { get; set; }

    internal Func<IRunContext, Task<string>> CacheInvalidator { get; set; }

    internal Func<IRunContext, Task<AbsolutePath[]>>? CachePaths { get; set; }

    internal Func<IRunContext, Task>? Execute { get; set; }

    internal Func<IRunContext, Task<bool>>? Condition { get; set; }

    internal Func<IRunContext, Task<RunnerOS>>? RunnerOS { get; set; }

    internal IRunContext? RunContext { get; set; }

    internal async Task<string> GetName() => ValueHelpers.GetOrNullFail(await Name(ValueHelpers.GetOrNullFail(RunContext)));

    internal async Task<string> GetCacheInvalidator() => ValueHelpers.GetOrNullFail(await CacheInvalidator(ValueHelpers.GetOrNullFail(RunContext)));

    internal Task<AbsolutePath[]> GetCachePaths() => CachePaths?.Invoke(ValueHelpers.GetOrNullFail(RunContext)) ?? Task.FromResult(Array.Empty<AbsolutePath>());

    internal Task GetExecute() => Execute?.Invoke(ValueHelpers.GetOrNullFail(RunContext)) ?? Task.CompletedTask;

    internal Task<bool> GetCondition() => Condition?.Invoke(ValueHelpers.GetOrNullFail(RunContext)) ?? Task.FromResult(true);

    internal async Task<RunnerOS> GetRunnerOS() => ValueHelpers.GetOrNullFail(await ValueHelpers.GetOrNullFail(RunnerOS).Invoke(ValueHelpers.GetOrNullFail(RunContext)));
}
