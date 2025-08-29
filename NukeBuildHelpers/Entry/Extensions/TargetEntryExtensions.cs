using NukeBuildHelpers.Entry.Interfaces;

namespace NukeBuildHelpers.Entry.Extensions;

/// <summary>
/// Extension methods for <see cref="IRunEntryDefinition"/>.
/// </summary>
public static class RunEntryExtensions
{
    /// <summary>
    /// Sets the application ID for the run entry definition.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of run entry definition.</typeparam>
    /// <param name="definition">The run entry definition instance.</param>
    /// <param name="appId">The application ID to set.</param>
    /// <returns>The modified run entry definition instance.</returns>
    public static TRunEntryDefinition AppId<TRunEntryDefinition>(this TRunEntryDefinition definition, string appId)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.AppIds = [appId];
        return definition;
    }

    /// <summary>
    /// Sets the application ID for the run entry definition.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of run entry definition.</typeparam>
    /// <param name="definition">The run entry definition instance.</param>
    /// <param name="appIds">The application IDs to set.</param>
    /// <returns>The modified run entry definition instance.</returns>
    public static TRunEntryDefinition AppId<TRunEntryDefinition>(this TRunEntryDefinition definition, params string[] appIds)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.AppIds = [.. appIds];
        return definition;
    }
}
