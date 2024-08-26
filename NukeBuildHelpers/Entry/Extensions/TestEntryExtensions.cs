using NukeBuildHelpers.Entry.Interfaces;

namespace NukeBuildHelpers.Entry.Extensions;

/// <summary>
/// Extension methods for <see cref="ITestEntryDefinition"/>.
/// </summary>
public static class TestEntryExtensions
{
    /// <summary>
    /// Configures whether to execute a task before the build in the workflow.
    /// </summary>
    /// <typeparam name="TTestEntryDefinition">The type of the workflow entry definition.</typeparam>
    /// <param name="definition">The instance of the entry definition.</param>
    /// <param name="executeBeforeBuild">A value indicating whether to execute a task before the build.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TTestEntryDefinition ExecuteBeforeBuild<TTestEntryDefinition>(this TTestEntryDefinition definition, bool executeBeforeBuild)
        where TTestEntryDefinition : ITestEntryDefinition
    {
        definition.ExecuteBeforeBuild = () => Task.Run(() => executeBeforeBuild);
        return definition;
    }

    /// <summary>
    /// Configures whether to execute a task before the build in the workflow using a function.
    /// </summary>
    /// <typeparam name="TTestEntryDefinition">The type of the workflow entry definition.</typeparam>
    /// <param name="definition">The instance of the entry definition.</param>
    /// <param name="executeBeforeBuild">A function that returns a value indicating whether to execute a task before the build.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TTestEntryDefinition ExecuteBeforeBuild<TTestEntryDefinition>(this TTestEntryDefinition definition, Func<bool> executeBeforeBuild)
        where TTestEntryDefinition : ITestEntryDefinition
    {
        definition.ExecuteBeforeBuild = () => Task.Run(() => executeBeforeBuild());
        return definition;
    }

    /// <summary>
    /// Configures whether to execute a task before the build in the workflow using an asynchronous function.
    /// </summary>
    /// <typeparam name="TTestEntryDefinition">The type of the workflow entry definition.</typeparam>
    /// <param name="definition">The instance of the entry definition.</param>
    /// <param name="executeBeforeBuild">An asynchronous function that returns a value indicating whether to execute a task before the build.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TTestEntryDefinition ExecuteBeforeBuild<TTestEntryDefinition>(this TTestEntryDefinition definition, Func<Task<bool>> executeBeforeBuild)
        where TTestEntryDefinition : ITestEntryDefinition
    {
        definition.ExecuteBeforeBuild = () => Task.Run(async () => await executeBeforeBuild());
        return definition;
    }
}
