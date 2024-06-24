using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;
using NukeBuildHelpers.Runner.Abstraction;

namespace NukeBuildHelpers.Entry.Extensions;

public static class EntryExtensions
{
    public static TEntryDefinition WorkflowBuilder<TEntryDefinition>(this TEntryDefinition definition, Action<IWorkflowBuilder> workflowBuilder)
        where TEntryDefinition : IEntryDefinition
    {
        definition.WorkflowBuilder = wb => Task.Run(() => workflowBuilder(wb));
        return definition;
    }

    public static TEntryDefinition WorkflowBuilder<TEntryDefinition>(this TEntryDefinition definition, Func<IWorkflowBuilder, Task> workflowBuilder)
        where TEntryDefinition : IEntryDefinition
    {
        definition.WorkflowBuilder = wb => Task.Run(async () => await workflowBuilder(wb));
        return definition;
    }

    public static TEntryDefinition WorkflowBuilder<TEntryDefinition, T>(this TEntryDefinition definition, Func<IWorkflowBuilder, Task<T>> workflowBuilder)
        where TEntryDefinition : IEntryDefinition
    {
        definition.WorkflowBuilder = wb => Task.Run(async () => await workflowBuilder(wb));
        return definition;
    }

    public static TEntryDefinition Name<TEntryDefinition>(this TEntryDefinition definition, string name)
        where TEntryDefinition : IEntryDefinition
    {
        definition.Name = _ => Task.Run(() => name);
        return definition;
    }

    public static TEntryDefinition Name<TEntryDefinition>(this TEntryDefinition definition, Func<string> name)
        where TEntryDefinition : IEntryDefinition
    {
        definition.Name = _ => Task.Run(() => name());
        return definition;
    }

    public static TEntryDefinition Name<TEntryDefinition>(this TEntryDefinition definition, Func<Task<string>> name)
        where TEntryDefinition : IEntryDefinition
    {
        definition.Name = _ => Task.Run(async () => await name());
        return definition;
    }

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
        definition.Execute = _ => Task.Run(() => action());
        return definition;
    }

    public static TEntryDefinition Execute<TEntryDefinition, T>(this TEntryDefinition definition, Func<T> action)
        where TEntryDefinition : IEntryDefinition
    {
        definition.Execute = _ => Task.Run(() => action());
        return definition;
    }

    public static TEntryDefinition Execute<TEntryDefinition>(this TEntryDefinition definition, Func<Task> action)
        where TEntryDefinition : IEntryDefinition
    {
        definition.Execute = _ => Task.Run(async () => await action());
        return definition;
    }

    public static TEntryDefinition Execute<TEntryDefinition, T>(this TEntryDefinition definition, Func<Task<T>> action)
        where TEntryDefinition : IEntryDefinition
    {
        definition.Execute = _ => Task.Run(async () => await action());
        return definition;
    }

    public static TEntryDefinition Execute<TEntryDefinition>(this TEntryDefinition definition, Action<IRunContext> action)
        where TEntryDefinition : IEntryDefinition
    {
        definition.Execute = runContext => Task.Run(() => action(runContext));
        return definition;
    }

    public static TEntryDefinition Execute<TEntryDefinition>(this TEntryDefinition definition, Func<IRunContext, Task> action)
        where TEntryDefinition : IEntryDefinition
    {
        definition.Execute = runContext => Task.Run(async () => await action(runContext));
        return definition;
    }

    public static TEntryDefinition Execute<TEntryDefinition, T>(this TEntryDefinition definition, Func<IRunContext, Task<T>> action)
        where TEntryDefinition : IEntryDefinition
    {
        definition.Execute = runContext => Task.Run(async () => await action(runContext));
        return definition;
    }
}
