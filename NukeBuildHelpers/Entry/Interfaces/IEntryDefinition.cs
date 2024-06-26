using Nuke.Common.IO;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;
using NukeBuildHelpers.Runner.Abstraction;

namespace NukeBuildHelpers.Entry.Interfaces;

/// <summary>
/// Interface defining the definition of an entry in the build system.
/// </summary>
public interface IEntryDefinition
{
    internal string Id { get; set; }

    internal Func<IWorkflowBuilder, Task>? WorkflowBuilder { get; set; }

    internal Func<IWorkflowBuilder, Task<string>> DisplayName { get; set; }

    internal Func<IRunContext, Task<bool>> Condition { get; set; }

    internal Func<IRunContext, Task<string>> CacheInvalidator { get; set; }

    internal Func<IRunContext, Task<AbsolutePath[]>>? CachePaths { get; set; }

    internal Func<IRunContext, Task>? Execute { get; set; }

    internal Func<IRunContext, Task<RunnerOS>>? RunnerOS { get; set; }

    internal IRunContext? RunContext { get; set; }

    internal Task GetWorkflowBuilder(IWorkflowBuilder workflowBuilder) => WorkflowBuilder?.Invoke(workflowBuilder) ?? Task.CompletedTask;

    internal async Task<string> GetDisplayName(IWorkflowBuilder workflowBuilder) => ValueHelpers.GetOrNullFail(await DisplayName(workflowBuilder));

    internal Task<bool> GetCondition() => Condition.Invoke(ValueHelpers.GetOrNullFail(RunContext));

    internal async Task<string> GetCacheInvalidator() => ValueHelpers.GetOrNullFail(await CacheInvalidator(ValueHelpers.GetOrNullFail(RunContext)));

    internal Task<AbsolutePath[]> GetCachePaths() => CachePaths?.Invoke(ValueHelpers.GetOrNullFail(RunContext)) ?? Task.FromResult(Array.Empty<AbsolutePath>());

    internal Task GetExecute() => Execute?.Invoke(ValueHelpers.GetOrNullFail(RunContext)) ?? Task.CompletedTask;

    internal async Task<RunnerOS> GetRunnerOS() => ValueHelpers.GetOrNullFail(await ValueHelpers.GetOrNullFail(RunnerOS).Invoke(ValueHelpers.GetOrNullFail(RunContext)));
}
