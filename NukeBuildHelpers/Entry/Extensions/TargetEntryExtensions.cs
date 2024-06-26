using NukeBuildHelpers.Entry.Interfaces;

namespace NukeBuildHelpers.Entry.Extensions;

/// <summary>
/// Extension methods for <see cref="ITargetEntryDefinition"/>.
/// </summary>
public static class TargetEntryExtensions
{
    /// <summary>
    /// Sets the application ID for the target entry definition.
    /// </summary>
    /// <typeparam name="TTargetEntryDefinition">The type of target entry definition.</typeparam>
    /// <param name="definition">The target entry definition instance.</param>
    /// <param name="appId">The application ID to set.</param>
    /// <returns>The modified target entry definition instance.</returns>
    public static TTargetEntryDefinition AppId<TTargetEntryDefinition>(this TTargetEntryDefinition definition, string appId)
        where TTargetEntryDefinition : ITargetEntryDefinition
    {
        definition.AppId = appId;
        return definition;
    }
}
