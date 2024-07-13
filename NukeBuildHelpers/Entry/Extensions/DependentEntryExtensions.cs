using NukeBuildHelpers.Entry.Interfaces;

namespace NukeBuildHelpers.Entry.Extensions;

/// <summary>
/// Extension methods for <see cref="IDependentEntryDefinition"/>.
/// </summary>
public static class DependentEntryExtensions
{
    /// <summary>
    /// Sets the array of application IDs for the dependent entry definition.
    /// </summary>
    /// <typeparam name="TDependentEntryDefinition">The type of the dependent entry definition.</typeparam>
    /// <param name="definition">The dependent entry definition to modify.</param>
    /// <param name="appIds">The array of application IDs.</param>
    /// <returns>The modified dependent entry definition.</returns>
    public static TDependentEntryDefinition AppId<TDependentEntryDefinition>(this TDependentEntryDefinition definition, params string[] appIds)
        where TDependentEntryDefinition : ITestEntryDefinition
    {
        definition.AppIds.AddRange(appIds);
        return definition;
    }
}
