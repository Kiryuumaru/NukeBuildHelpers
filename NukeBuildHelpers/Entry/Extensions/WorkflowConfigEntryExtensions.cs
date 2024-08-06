using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Runner.Abstraction;

namespace NukeBuildHelpers.Entry.Extensions;

/// <summary>
/// Extension methods for <see cref="IWorkflowConfigEntryDefinition"/> to configure various aspects of the entry.
/// </summary>
public static class WorkflowConfigEntryExtensions
{
    /// <summary>
    /// Sets the display name for the workflow to generate.
    /// </summary>
    /// <typeparam name="TWorkflowConfigEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="name">The display name to set.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TWorkflowConfigEntryDefinition Name<TWorkflowConfigEntryDefinition>(this TWorkflowConfigEntryDefinition definition, string name)
        where TWorkflowConfigEntryDefinition : IWorkflowConfigEntryDefinition
    {
        definition.Name = () => Task.Run(() => name);
        return definition;
    }

    /// <summary>
    /// Sets the display name using a function for the workflow to generate.
    /// </summary>
    /// <typeparam name="TWorkflowConfigEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="name">The function returning the display name.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TWorkflowConfigEntryDefinition Name<TWorkflowConfigEntryDefinition>(this TWorkflowConfigEntryDefinition definition, Func<string> name)
        where TWorkflowConfigEntryDefinition : IWorkflowConfigEntryDefinition
    {
        definition.Name = () => Task.Run(() => name());
        return definition;
    }

    /// <summary>
    /// Sets the display name using an asynchronous function for the workflow to generate.
    /// </summary>
    /// <typeparam name="TWorkflowConfigEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="name">The asynchronous function returning the display name.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TWorkflowConfigEntryDefinition Name<TWorkflowConfigEntryDefinition>(this TWorkflowConfigEntryDefinition definition, Func<Task<string>> name)
        where TWorkflowConfigEntryDefinition : IWorkflowConfigEntryDefinition
    {
        definition.Name = () => Task.Run(async () => await name());
        return definition;
    }

    /// <summary>
    /// Sets the pre setup runner OS for the workflow to generate.
    /// </summary>
    /// <typeparam name="TWorkflowConfigEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="runnerOS">The pre setup runner OS to set.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TWorkflowConfigEntryDefinition PreSetupRunnerOS<TWorkflowConfigEntryDefinition>(this TWorkflowConfigEntryDefinition definition, RunnerOS runnerOS)
        where TWorkflowConfigEntryDefinition : IWorkflowConfigEntryDefinition
    {
        definition.PreSetupRunnerOS = () => Task.Run(() => runnerOS);
        return definition;
    }

    /// <summary>
    /// Sets the pre setup runner OS using a function for the workflow to generate.
    /// </summary>
    /// <typeparam name="TWorkflowConfigEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="runnerOS">The function returning the pre setup runner OS.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TWorkflowConfigEntryDefinition PreSetupRunnerOS<TWorkflowConfigEntryDefinition>(this TWorkflowConfigEntryDefinition definition, Func<RunnerOS> runnerOS)
        where TWorkflowConfigEntryDefinition : IWorkflowConfigEntryDefinition
    {
        definition.PreSetupRunnerOS = () => Task.Run(() => runnerOS());
        return definition;
    }

    /// <summary>
    /// Sets the pre setup runner OS using an asynchronous function for the workflow to generate.
    /// </summary>
    /// <typeparam name="TWorkflowConfigEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="runnerOS">The asynchronous function returning the pre setup runner OS.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TWorkflowConfigEntryDefinition PreSetupRunnerOS<TWorkflowConfigEntryDefinition>(this TWorkflowConfigEntryDefinition definition, Func<Task<RunnerOS>> runnerOS)
        where TWorkflowConfigEntryDefinition : IWorkflowConfigEntryDefinition
    {
        definition.PreSetupRunnerOS = () => Task.Run(async () => await runnerOS());
        return definition;
    }

    /// <summary>
    /// Sets the post setup runner OS for the workflow to generate.
    /// </summary>
    /// <typeparam name="TWorkflowConfigEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="runnerOS">The post setup runner OS to set.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TWorkflowConfigEntryDefinition PostSetupRunnerOS<TWorkflowConfigEntryDefinition>(this TWorkflowConfigEntryDefinition definition, RunnerOS runnerOS)
        where TWorkflowConfigEntryDefinition : IWorkflowConfigEntryDefinition
    {
        definition.PostSetupRunnerOS = () => Task.Run(() => runnerOS);
        return definition;
    }

    /// <summary>
    /// Sets the post setup runner OS using a function for the workflow to generate.
    /// </summary>
    /// <typeparam name="TWorkflowConfigEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="runnerOS">The function returning the post setup runner OS.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TWorkflowConfigEntryDefinition PostSetupRunnerOS<TWorkflowConfigEntryDefinition>(this TWorkflowConfigEntryDefinition definition, Func<RunnerOS> runnerOS)
        where TWorkflowConfigEntryDefinition : IWorkflowConfigEntryDefinition
    {
        definition.PostSetupRunnerOS = () => Task.Run(() => runnerOS());
        return definition;
    }

    /// <summary>
    /// Sets the post setup runner OS using an asynchronous function for the workflow to generate.
    /// </summary>
    /// <typeparam name="TWorkflowConfigEntryDefinition">The type of entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="runnerOS">The asynchronous function returning the post setup runner OS.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TWorkflowConfigEntryDefinition PostSetupRunnerOS<TWorkflowConfigEntryDefinition>(this TWorkflowConfigEntryDefinition definition, Func<Task<RunnerOS>> runnerOS)
        where TWorkflowConfigEntryDefinition : IWorkflowConfigEntryDefinition
    {
        definition.PostSetupRunnerOS = () => Task.Run(async () => await runnerOS());
        return definition;
    }
}
