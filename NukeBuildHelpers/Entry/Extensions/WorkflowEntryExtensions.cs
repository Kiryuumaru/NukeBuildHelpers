using Nuke.Common.IO;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;
using NukeBuildHelpers.Runner.Abstraction;

namespace NukeBuildHelpers.Entry.Extensions;

/// <summary>
/// Extension methods for <see cref="IEntryDefinition"/> to configure various aspects of the entry.
/// </summary>
public static class WorkflowEntryExtensions
{
    /// <summary>
    /// Configures the workflow builder function for this entry definition.
    /// </summary>
    /// <typeparam name="TEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="workflowBuilder">The action to configure the workflow builder.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TEntryDefinition WorkflowBuilder<TEntryDefinition>(this TEntryDefinition definition, Action<IWorkflowBuilder> workflowBuilder)
        where TEntryDefinition : IEntryDefinition
    {
        definition.WorkflowBuilder.Add(wb => Task.Run(() => workflowBuilder(wb)));
        return definition;
    }

    /// <summary>
    /// Configures the asynchronous workflow builder function for this entry definition.
    /// </summary>
    /// <typeparam name="TEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="workflowBuilder">The asynchronous action to configure the workflow builder.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TEntryDefinition WorkflowBuilder<TEntryDefinition>(this TEntryDefinition definition, Func<IWorkflowBuilder, Task> workflowBuilder)
        where TEntryDefinition : IEntryDefinition
    {
        definition.WorkflowBuilder.Add(wb => Task.Run(async () => await workflowBuilder(wb)));
        return definition;
    }

    /// <summary>
    /// Configures the asynchronous workflow builder function with a return value for this entry definition.
    /// </summary>
    /// <typeparam name="TEntryDefinition">The type of entry definition.</typeparam>
    /// <typeparam name="T">The type of the return value.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="workflowBuilder">The asynchronous function to configure the workflow builder.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TEntryDefinition WorkflowBuilder<TEntryDefinition, T>(this TEntryDefinition definition, Func<IWorkflowBuilder, Task<T>> workflowBuilder)
        where TEntryDefinition : IEntryDefinition
    {
        definition.WorkflowBuilder.Add(wb => Task.Run(async () => await workflowBuilder(wb)));
        return definition;
    }

    /// <summary>
    /// Sets the display name for this entry definition.
    /// </summary>
    /// <typeparam name="TEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="displayName">The display name to set.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TEntryDefinition DisplayName<TEntryDefinition>(this TEntryDefinition definition, string displayName)
        where TEntryDefinition : IEntryDefinition
    {
        definition.DisplayName = _ => Task.Run(() => displayName);
        return definition;
    }

    /// <summary>
    /// Sets the display name using a function for this entry definition.
    /// </summary>
    /// <typeparam name="TEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="displayName">The function returning the display name.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TEntryDefinition DisplayName<TEntryDefinition>(this TEntryDefinition definition, Func<string> displayName)
        where TEntryDefinition : IEntryDefinition
    {
        definition.DisplayName = _ => Task.Run(() => displayName());
        return definition;
    }

    /// <summary>
    /// Sets the display name using an asynchronous function for this entry definition.
    /// </summary>
    /// <typeparam name="TEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="displayName">The asynchronous function returning the display name.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TEntryDefinition DisplayName<TEntryDefinition>(this TEntryDefinition definition, Func<Task<string>> displayName)
        where TEntryDefinition : IEntryDefinition
    {
        definition.DisplayName = _ => Task.Run(async () => await displayName());
        return definition;
    }
}
