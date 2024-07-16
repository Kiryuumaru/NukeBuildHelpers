using Nuke.Common.IO;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;
using NukeBuildHelpers.Runner.Abstraction;

namespace NukeBuildHelpers.Entry.Extensions;

/// <summary>
/// Extension methods for <see cref="IRunEntryDefinition"/> to configure various aspects of the entry.
/// </summary>
public static class WorkflowRunEntryExtensions
{
    /// <summary>
    /// Sets the workflow id for this entry definition.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="workflowId">The workflow id to set.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TRunEntryDefinition WorkflowId<TRunEntryDefinition>(this TRunEntryDefinition definition, string workflowId)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.Id = workflowId;
        return definition;
    }

    /// <summary>
    /// Configures the workflow builder function for this entry definition.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="workflowBuilder">The action to configure the workflow builder.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TRunEntryDefinition WorkflowBuilder<TRunEntryDefinition>(this TRunEntryDefinition definition, Action<IWorkflowBuilder> workflowBuilder)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.WorkflowBuilder.Add(wb => Task.Run(() => workflowBuilder(wb)));
        return definition;
    }

    /// <summary>
    /// Configures the asynchronous workflow builder function for this entry definition.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="workflowBuilder">The asynchronous action to configure the workflow builder.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TRunEntryDefinition WorkflowBuilder<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<IWorkflowBuilder, Task> workflowBuilder)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.WorkflowBuilder.Add(wb => Task.Run(async () => await workflowBuilder(wb)));
        return definition;
    }

    /// <summary>
    /// Configures the asynchronous workflow builder function with a return value for this entry definition.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of entry definition.</typeparam>
    /// <typeparam name="T">The type of the return value.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="workflowBuilder">The asynchronous function to configure the workflow builder.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TRunEntryDefinition WorkflowBuilder<TRunEntryDefinition, T>(this TRunEntryDefinition definition, Func<IWorkflowBuilder, Task<T>> workflowBuilder)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.WorkflowBuilder.Add(wb => Task.Run(async () => await workflowBuilder(wb)));
        return definition;
    }

    /// <summary>
    /// Sets the display name for this entry definition.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="displayName">The display name to set.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TRunEntryDefinition DisplayName<TRunEntryDefinition>(this TRunEntryDefinition definition, string displayName)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.DisplayName = _ => Task.Run(() => displayName);
        return definition;
    }

    /// <summary>
    /// Sets the display name using a function for this entry definition.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="displayName">The function returning the display name.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TRunEntryDefinition DisplayName<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<string> displayName)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.DisplayName = _ => Task.Run(() => displayName());
        return definition;
    }

    /// <summary>
    /// Sets the display name using an asynchronous function for this entry definition.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="displayName">The asynchronous function returning the display name.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TRunEntryDefinition DisplayName<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<Task<string>> displayName)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.DisplayName = _ => Task.Run(async () => await displayName());
        return definition;
    }
}
