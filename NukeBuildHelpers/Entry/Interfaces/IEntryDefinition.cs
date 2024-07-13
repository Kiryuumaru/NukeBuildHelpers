using Nuke.Common.IO;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Entry.Enums;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;
using NukeBuildHelpers.Runner.Abstraction;

namespace NukeBuildHelpers.Entry.Interfaces;

/// <summary>
/// Interface defining the definition of an entry in the build system.
/// </summary>
public interface IEntryDefinition
{
    internal IEntryDefinition Clone();

    internal string Id { get; set; }

    internal Func<IWorkflowBuilder, Task<string>> DisplayName { get; set; }

    internal Func<IRunContext, Task<bool>> Condition { get; set; }

    internal Func<IRunContext, Task<RunnerOS>>? RunnerOS { get; set; }

    internal List<Func<IRunContext, Task<AbsolutePath[]>>> CachePath { get; set; }

    internal Func<IRunContext, Task<string>> CacheInvalidator { get; set; }

    internal Func<IRunContext, Task<int>> CheckoutFetchDepth { get; set; }

    internal Func<IRunContext, Task<bool>> CheckoutFetchTags { get; set; }

    internal Func<IRunContext, Task<SubmoduleCheckoutType>> CheckoutSubmodules { get; set; }

    internal List<Func<IRunContext, Task>> Execute { get; set; }

    internal List<Func<IWorkflowBuilder, Task>> WorkflowBuilder { get; set; }

    internal List<Func<IEntryDefinition, Task<IEntryDefinition[]>>> Matrix { get; set; }

    internal IRunContext? RunContext { get; set; }

    internal async Task<string> GetDisplayName(IWorkflowBuilder workflowBuilder)
    {
        return ValueHelpers.GetOrNullFail(await DisplayName(workflowBuilder));
    }

    internal Task<bool> GetCondition()
    {
        return Condition.Invoke(ValueHelpers.GetOrNullFail(RunContext));
    }

    internal async Task<RunnerOS> GetRunnerOS()
    {
        return ValueHelpers.GetOrNullFail(await ValueHelpers.GetOrNullFail(RunnerOS).Invoke(ValueHelpers.GetOrNullFail(RunContext)));
    }

    internal async Task<AbsolutePath[]> GetCachePaths()
    {
        List<AbsolutePath> cachePaths = [];
        foreach (var cachePath in CachePath)
        {
            cachePaths.AddRange(await cachePath(ValueHelpers.GetOrNullFail(RunContext)));
        }
        return [.. cachePaths];
    }

    internal async Task<string> GetCacheInvalidator()
    {
        return ValueHelpers.GetOrNullFail(await CacheInvalidator(ValueHelpers.GetOrNullFail(RunContext)));
    }

    internal async Task<int> GetCheckoutFetchDepth()
    {
        return await CheckoutFetchDepth(ValueHelpers.GetOrNullFail(RunContext));
    }

    internal async Task<bool> GetCheckoutFetchTags()
    {
        return await CheckoutFetchTags(ValueHelpers.GetOrNullFail(RunContext));
    }

    internal async Task<SubmoduleCheckoutType> GetCheckoutSubmodules()
    {
        return await CheckoutSubmodules(ValueHelpers.GetOrNullFail(RunContext));
    }

    internal async Task GetExecute()
    {
        foreach (var execute in Execute)
        {
            await execute(ValueHelpers.GetOrNullFail(RunContext));
        }
    }

    internal async Task GetWorkflowBuilder(IWorkflowBuilder workflowBuilder)
    {
        foreach (var builder in WorkflowBuilder)
        {
            await builder(workflowBuilder);
        }
    }
}
