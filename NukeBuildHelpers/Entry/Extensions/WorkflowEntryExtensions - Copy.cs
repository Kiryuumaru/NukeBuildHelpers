using Nuke.Common.IO;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;
using NukeBuildHelpers.Runner.Abstraction;

namespace NukeBuildHelpers.Entry.Extensions;

public static class WorkflowEntryExtensions
{
    public static TEntryDefinition WorkflowBuilder<TEntryDefinition>(this TEntryDefinition definition, Action<IWorkflowBuilder> workflowBuilder)
        where TEntryDefinition : IEntryDefinition
    {
        definition.WorkflowBuilder.Add(wb => Task.Run(() => workflowBuilder(wb)));
        return definition;
    }

    public static TEntryDefinition WorkflowBuilder<TEntryDefinition>(this TEntryDefinition definition, Func<IWorkflowBuilder, Task> workflowBuilder)
        where TEntryDefinition : IEntryDefinition
    {
        definition.WorkflowBuilder.Add(wb => Task.Run(async () => await workflowBuilder(wb)));
        return definition;
    }

    public static TEntryDefinition WorkflowBuilder<TEntryDefinition, T>(this TEntryDefinition definition, Func<IWorkflowBuilder, Task<T>> workflowBuilder)
        where TEntryDefinition : IEntryDefinition
    {
        definition.WorkflowBuilder.Add(wb => Task.Run(async () => await workflowBuilder(wb)));
        return definition;
    }

    public static TEntryDefinition DisplayName<TEntryDefinition>(this TEntryDefinition definition, string displayName)
        where TEntryDefinition : IEntryDefinition
    {
        definition.DisplayName = _ => Task.Run(() => displayName);
        return definition;
    }

    public static TEntryDefinition DisplayName<TEntryDefinition>(this TEntryDefinition definition, Func<string> displayName)
        where TEntryDefinition : IEntryDefinition
    {
        definition.DisplayName = _ => Task.Run(() => displayName());
        return definition;
    }

    public static TEntryDefinition DisplayName<TEntryDefinition>(this TEntryDefinition definition, Func<Task<string>> displayName)
        where TEntryDefinition : IEntryDefinition
    {
        definition.DisplayName = _ => Task.Run(async () => await displayName());
        return definition;
    }
}

public static class TargetEntryExtensions
{
    public static TTargetEntryDefinition AppId<TTargetEntryDefinition>(this TTargetEntryDefinition definition, string appId)
        where TTargetEntryDefinition : ITargetEntryDefinition
    {
        definition.AppId = appId;
        return definition;
    }
}

public static class ExecutableEntryExtensions
{
    public static TEntryDefinition Condition<TEntryDefinition>(this TEntryDefinition definition, bool condition)
        where TEntryDefinition : IEntryDefinition
    {
        definition.Condition = _ => Task.Run(() => condition);
        return definition;
    }

    public static TEntryDefinition Condition<TEntryDefinition>(this TEntryDefinition definition, Func<bool> condition)
        where TEntryDefinition : IEntryDefinition
    {
        definition.Condition = _ => Task.Run(() => condition());
        return definition;
    }

    public static TEntryDefinition Condition<TEntryDefinition>(this TEntryDefinition definition, Func<IRunContext, bool> condition)
        where TEntryDefinition : IEntryDefinition
    {
        definition.Condition = runContext => Task.Run(() => condition(runContext));
        return definition;
    }

    public static TEntryDefinition Condition<TEntryDefinition>(this TEntryDefinition definition, Func<Task<bool>> condition)
        where TEntryDefinition : IEntryDefinition
    {
        definition.Condition = _ => Task.Run(async () => await condition());
        return definition;
    }

    public static TEntryDefinition Condition<TEntryDefinition>(this TEntryDefinition definition, Func<IRunContext, Task<bool>> condition)
        where TEntryDefinition : IEntryDefinition
    {
        definition.Condition = runContext => Task.Run(async () => await condition(runContext));
        return definition;
    }

    public static TEntryDefinition RunnerOS<TEntryDefinition>(this TEntryDefinition definition, RunnerOS runnerOS)
        where TEntryDefinition : IEntryDefinition
    {
        definition.RunnerOS = _ => Task.Run(() => runnerOS);
        return definition;
    }

    public static TEntryDefinition RunnerOS<TEntryDefinition>(this TEntryDefinition definition, Func<RunnerOS> runnerOS)
        where TEntryDefinition : IEntryDefinition
    {
        definition.RunnerOS = _ => Task.Run(() => runnerOS());
        return definition;
    }

    public static TEntryDefinition RunnerOS<TEntryDefinition>(this TEntryDefinition definition, Func<IRunContext, RunnerOS> runnerOS)
        where TEntryDefinition : IEntryDefinition
    {
        definition.RunnerOS = runContext => Task.Run(() => runnerOS(runContext));
        return definition;
    }

    public static TEntryDefinition RunnerOS<TEntryDefinition>(this TEntryDefinition definition, Func<Task<RunnerOS>> runnerOS)
        where TEntryDefinition : IEntryDefinition
    {
        definition.RunnerOS = _ => Task.Run(async () => await runnerOS());
        return definition;
    }

    public static TEntryDefinition RunnerOS<TEntryDefinition>(this TEntryDefinition definition, Func<IRunContext, Task<RunnerOS>> runnerOS)
        where TEntryDefinition : IEntryDefinition
    {
        definition.RunnerOS = runContext => Task.Run(async () => await runnerOS(runContext));
        return definition;
    }

    public static TEntryDefinition Execute<TEntryDefinition>(this TEntryDefinition definition, Action action)
        where TEntryDefinition : IEntryDefinition
    {
        definition.Execute.Add(_ => Task.Run(() => action()));
        return definition;
    }

    public static TEntryDefinition Execute<TEntryDefinition, T>(this TEntryDefinition definition, Func<T> action)
        where TEntryDefinition : IEntryDefinition
    {
        definition.Execute.Add(_ => Task.Run(() => action()));
        return definition;
    }

    public static TEntryDefinition Execute<TEntryDefinition>(this TEntryDefinition definition, Func<Task> action)
        where TEntryDefinition : IEntryDefinition
    {
        definition.Execute.Add(_ => Task.Run(async () => await action()));
        return definition;
    }

    public static TEntryDefinition Execute<TEntryDefinition, T>(this TEntryDefinition definition, Func<Task<T>> action)
        where TEntryDefinition : IEntryDefinition
    {
        definition.Execute.Add(_ => Task.Run(async () => await action()));
        return definition;
    }

    public static TEntryDefinition Execute<TEntryDefinition>(this TEntryDefinition definition, Action<IRunContext> action)
        where TEntryDefinition : IEntryDefinition
    {
        definition.Execute.Add(runContext => Task.Run(() => action(runContext)));
        return definition;
    }

    public static TEntryDefinition Execute<TEntryDefinition>(this TEntryDefinition definition, Func<IRunContext, Task> action)
        where TEntryDefinition : IEntryDefinition
    {
        definition.Execute.Add(runContext => Task.Run(async () => await action(runContext)));
        return definition;
    }

    public static TEntryDefinition Execute<TEntryDefinition, T>(this TEntryDefinition definition, Func<IRunContext, Task<T>> action)
        where TEntryDefinition : IEntryDefinition
    {
        definition.Execute.Add(runContext => Task.Run(async () => await action(runContext)));
        return definition;
    }
}

public static class DependentEntryExtensions
{
    public static TDependentEntryDefinition AppId<TDependentEntryDefinition>(this TDependentEntryDefinition definition, params string[] appIds)
        where TDependentEntryDefinition : ITestEntryDefinition
    {
        definition.AppIds = appIds;
        return definition;
    }
}

public static class CacheEntryExtensions
{
    public static TEntryDefinition CachePath<TEntryDefinition>(this TEntryDefinition definition, params AbsolutePath[] cachePath)
        where TEntryDefinition : IEntryDefinition
    {
        definition.CachePath.Add(_ => Task.Run(() => cachePath));
        return definition;
    }

    public static TEntryDefinition CachePath<TEntryDefinition>(this TEntryDefinition definition, Func<AbsolutePath[]> cachePaths)
        where TEntryDefinition : IEntryDefinition
    {
        definition.CachePath.Add(_ => Task.Run(() => cachePaths()));
        return definition;
    }

    public static TEntryDefinition CachePath<TEntryDefinition>(this TEntryDefinition definition, Func<IRunContext, AbsolutePath[]> cachePaths)
        where TEntryDefinition : IEntryDefinition
    {
        definition.CachePath.Add(runContext => Task.Run(() => cachePaths(runContext)));
        return definition;
    }

    public static TEntryDefinition CachePath<TEntryDefinition>(this TEntryDefinition definition, Func<Task<AbsolutePath[]>> cachePaths)
        where TEntryDefinition : IEntryDefinition
    {
        definition.CachePath.Add(_ => Task.Run(async () => await cachePaths()));
        return definition;
    }

    public static TEntryDefinition CachePath<TEntryDefinition>(this TEntryDefinition definition, Func<IRunContext, Task<AbsolutePath[]>> cachePaths)
        where TEntryDefinition : IEntryDefinition
    {
        definition.CachePath.Add(runContext => Task.Run(async () => await cachePaths(runContext)));
        return definition;
    }

    public static TEntryDefinition CacheInvalidator<TEntryDefinition>(this TEntryDefinition definition, string cacheInvalidator)
        where TEntryDefinition : IEntryDefinition
    {
        definition.CacheInvalidator = _ => Task.Run(() => cacheInvalidator);
        return definition;
    }

    public static TEntryDefinition CacheInvalidator<TEntryDefinition>(this TEntryDefinition definition, Func<string> cacheInvalidator)
        where TEntryDefinition : IEntryDefinition
    {
        definition.CacheInvalidator = _ => Task.Run(() => cacheInvalidator());
        return definition;
    }

    public static TEntryDefinition CacheInvalidator<TEntryDefinition>(this TEntryDefinition definition, Func<IRunContext, string> cacheInvalidator)
        where TEntryDefinition : IEntryDefinition
    {
        definition.CacheInvalidator = runContext => Task.Run(() => cacheInvalidator(runContext));
        return definition;
    }

    public static TEntryDefinition CacheInvalidator<TEntryDefinition>(this TEntryDefinition definition, Func<Task<string>> cacheInvalidator)
        where TEntryDefinition : IEntryDefinition
    {
        definition.CacheInvalidator = _ => Task.Run(async () => await cacheInvalidator());
        return definition;
    }

    public static TEntryDefinition CacheInvalidator<TEntryDefinition>(this TEntryDefinition definition, Func<IRunContext, Task<string>> cacheInvalidator)
        where TEntryDefinition : IEntryDefinition
    {
        definition.CacheInvalidator = runContext => Task.Run(async () => await cacheInvalidator(runContext));
        return definition;
    }
}

public static class BuildEntryExtensions
{
    public static TBuildEntryDefinition ReleaseAsset<TBuildEntryDefinition>(this TBuildEntryDefinition definition, params AbsolutePath[] assets)
        where TBuildEntryDefinition : IBuildEntryDefinition
    {
        definition.ReleaseAsset.Add(_ => Task.Run(() => assets));
        return definition;
    }

    public static TBuildEntryDefinition ReleaseAsset<TBuildEntryDefinition>(this TBuildEntryDefinition definition, Func<AbsolutePath[]> assets)
        where TBuildEntryDefinition : IBuildEntryDefinition
    {
        definition.ReleaseAsset.Add(_ => Task.Run(() => assets()));
        return definition;
    }

    public static TBuildEntryDefinition ReleaseAsset<TBuildEntryDefinition>(this TBuildEntryDefinition definition, Func<IRunContext, AbsolutePath[]> assets)
        where TBuildEntryDefinition : IBuildEntryDefinition
    {
        definition.ReleaseAsset.Add(runContext => Task.Run(() => assets(runContext)));
        return definition;
    }

    public static TBuildEntryDefinition ReleaseAsset<TBuildEntryDefinition>(this TBuildEntryDefinition definition, Func<Task<AbsolutePath[]>> assets)
        where TBuildEntryDefinition : IBuildEntryDefinition
    {
        definition.ReleaseAsset.Add(_ => Task.Run(async () => await assets()));
        return definition;
    }

    public static TBuildEntryDefinition ReleaseAsset<TBuildEntryDefinition>(this TBuildEntryDefinition definition, Func<IRunContext, Task<AbsolutePath[]>> assets)
        where TBuildEntryDefinition : IBuildEntryDefinition
    {
        definition.ReleaseAsset.Add(runContext => Task.Run(async () => await assets(runContext)));
        return definition;
    }

    public static TBuildEntryDefinition CommonReleaseAsset<TBuildEntryDefinition>(this TBuildEntryDefinition definition, params AbsolutePath[] assets)
        where TBuildEntryDefinition : IBuildEntryDefinition
    {
        definition.CommonReleaseAsset.Add(_ => Task.Run(() => assets));
        return definition;
    }

    public static TBuildEntryDefinition CommonReleaseAsset<TBuildEntryDefinition>(this TBuildEntryDefinition definition, Func<AbsolutePath[]> assets)
        where TBuildEntryDefinition : IBuildEntryDefinition
    {
        definition.CommonReleaseAsset.Add(_ => Task.Run(() => assets()));
        return definition;
    }

    public static TBuildEntryDefinition CommonReleaseAsset<TBuildEntryDefinition>(this TBuildEntryDefinition definition, Func<IRunContext, AbsolutePath[]> assets)
        where TBuildEntryDefinition : IBuildEntryDefinition
    {
        definition.CommonReleaseAsset.Add(runContext => Task.Run(() => assets(runContext)));
        return definition;
    }

    public static TBuildEntryDefinition CommonReleaseAsset<TBuildEntryDefinition>(this TBuildEntryDefinition definition, Func<Task<AbsolutePath[]>> assets)
        where TBuildEntryDefinition : IBuildEntryDefinition
    {
        definition.CommonReleaseAsset.Add(_ => Task.Run(async () => await assets()));
        return definition;
    }

    public static TBuildEntryDefinition CommonReleaseAsset<TBuildEntryDefinition>(this TBuildEntryDefinition definition, Func<IRunContext, Task<AbsolutePath[]>> assets)
        where TBuildEntryDefinition : IBuildEntryDefinition
    {
        definition.CommonReleaseAsset.Add(runContext => Task.Run(async () => await assets(runContext)));
        return definition;
    }
}