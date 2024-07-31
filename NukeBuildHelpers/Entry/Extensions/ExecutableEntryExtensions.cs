using Nuke.Common.IO;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;
using NukeBuildHelpers.Runner.Abstraction;

namespace NukeBuildHelpers.Entry.Extensions;

/// <summary>
/// Extension methods for <see cref="IRunEntryDefinition"/> to configure various aspects of the entry.
/// </summary>
public static class ExecutableEntryExtensions
{
    /// <summary>
    /// Sets the condition for executing this entry definition.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="condition">The condition to set.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TRunEntryDefinition Condition<TRunEntryDefinition>(this TRunEntryDefinition definition, bool condition)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.Condition = _ => Task.Run(() => condition);
        return definition;
    }

    /// <summary>
    /// Sets the condition using a function for executing this entry definition.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="condition">The function returning the condition.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TRunEntryDefinition Condition<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<bool> condition)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.Condition = _ => Task.Run(() => condition());
        return definition;
    }

    /// <summary>
    /// Sets the condition using a function with run context for executing this entry definition.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="condition">The function with run context returning the condition.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TRunEntryDefinition Condition<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<IRunContext, bool> condition)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.Condition = runContext => Task.Run(() => condition(runContext));
        return definition;
    }

    /// <summary>
    /// Sets the condition using an asynchronous function for executing this entry definition.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="condition">The asynchronous function returning the condition.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TRunEntryDefinition Condition<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<Task<bool>> condition)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.Condition = _ => Task.Run(async () => await condition());
        return definition;
    }

    /// <summary>
    /// Sets the condition using an asynchronous function with run context for executing this entry definition.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="condition">The asynchronous function with run context returning the condition.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TRunEntryDefinition Condition<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<IRunContext, Task<bool>> condition)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.Condition = runContext => Task.Run(async () => await condition(runContext));
        return definition;
    }

    /// <summary>
    /// Sets the operating system for executing this entry definition.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="runnerOS">The operating system to set.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TRunEntryDefinition RunnerOS<TRunEntryDefinition>(this TRunEntryDefinition definition, RunnerOS runnerOS)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.RunnerOS = _ => Task.Run(() => runnerOS);
        return definition;
    }

    /// <summary>
    /// Sets the operating system using a function for executing this entry definition.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="runnerOS">The function returning the operating system.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TRunEntryDefinition RunnerOS<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<RunnerOS> runnerOS)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.RunnerOS = _ => Task.Run(() => runnerOS());
        return definition;
    }

    /// <summary>
    /// Sets the operating system using a function with run context for executing this entry definition.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="runnerOS">The function with run context returning the operating system.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TRunEntryDefinition RunnerOS<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<IRunContext, RunnerOS> runnerOS)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.RunnerOS = runContext => Task.Run(() => runnerOS(runContext));
        return definition;
    }

    /// <summary>
    /// Sets the operating system using an asynchronous function for executing this entry definition.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="runnerOS">The asynchronous function returning the operating system.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TRunEntryDefinition RunnerOS<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<Task<RunnerOS>> runnerOS)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.RunnerOS = _ => Task.Run(async () => await runnerOS());
        return definition;
    }

    /// <summary>
    /// Sets the operating system using an asynchronous function with run context for executing this entry definition.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="runnerOS">The asynchronous function with run context returning the operating system.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TRunEntryDefinition RunnerOS<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<IRunContext, Task<RunnerOS>> runnerOS)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.RunnerOS = runContext => Task.Run(async () => await runnerOS(runContext));
        return definition;
    }

    /// <summary>
    /// Sets the action to execute for this entry definition.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="action">The action to set.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TRunEntryDefinition Execute<TRunEntryDefinition>(this TRunEntryDefinition definition, Action action)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        var value = definition.Execute;
        value.Add(_ => Task.Run(() => action()));
        definition.Execute = value;
        return definition;
    }

    /// <summary>
    /// Sets the action to execute synchronously for this entry definition.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of entry definition.</typeparam>
    /// <typeparam name="T">The type of the return value.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="action">The action to set.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TRunEntryDefinition Execute<TRunEntryDefinition, T>(this TRunEntryDefinition definition, Func<T> action)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        var value = definition.Execute;
        value.Add(_ => Task.Run(() => action()));
        definition.Execute = value;
        return definition;
    }

    /// <summary>
    /// Sets the asynchronous action to execute for this entry definition.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="action">The asynchronous action to set.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TRunEntryDefinition Execute<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<Task> action)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        var value = definition.Execute;
        value.Add(_ => Task.Run(async () => await action()));
        definition.Execute = value;
        return definition;
    }

    /// <summary>
    /// Sets the asynchronous function to execute with a return value for this entry definition.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of entry definition.</typeparam>
    /// <typeparam name="T">The type of the return value.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="action">The asynchronous function to set.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TRunEntryDefinition Execute<TRunEntryDefinition, T>(this TRunEntryDefinition definition, Func<Task<T>> action)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        var value = definition.Execute;
        value.Add(_ => Task.Run(async () => await action()));
        definition.Execute = value;
        return definition;
    }

    /// <summary>
    /// Sets the action with run context to execute for this entry definition.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="action">The action with run context to set.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TRunEntryDefinition Execute<TRunEntryDefinition>(this TRunEntryDefinition definition, Action<IRunContext> action)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        var value = definition.Execute;
        value.Add(runContext => Task.Run(() => action(runContext)));
        definition.Execute = value;
        return definition;
    }

    /// <summary>
    /// Sets the asynchronous action with run context to execute for this entry definition.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="action">The asynchronous action with run context to set.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TRunEntryDefinition Execute<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<IRunContext, Task> action)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        var value = definition.Execute;
        value.Add(runContext => Task.Run(async () => await action(runContext)));
        definition.Execute = value;
        return definition;
    }

    /// <summary>
    /// Sets the asynchronous function with run context to execute with a return value for this entry definition.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of entry definition.</typeparam>
    /// <typeparam name="T">The type of the return value.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="action">The asynchronous function with run context to set.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TRunEntryDefinition Execute<TRunEntryDefinition, T>(this TRunEntryDefinition definition, Func<IRunContext, Task<T>> action)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        var value = definition.Execute;
        value.Add(runContext => Task.Run(async () => await action(runContext)));
        definition.Execute = value;
        return definition;
    }
}
