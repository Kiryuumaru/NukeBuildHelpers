using Nuke.Common.IO;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;

namespace NukeBuildHelpers.Entry.Extensions;

/// <summary>
/// Extension methods for <see cref="IPublishEntryDefinition"/>.
/// </summary>
public static class PublishEntryExtensions
{
    /// <summary>
    /// Adds release assets to the publish entry definition.
    /// </summary>
    /// <typeparam name="TPublishEntryDefinition">The type of the publish entry definition.</typeparam>
    /// <param name="definition">The publish entry definition.</param>
    /// <param name="assets">The release assets.</param>
    /// <returns>The modified publish entry definition.</returns>
    public static TPublishEntryDefinition ReleaseAsset<TPublishEntryDefinition>(this TPublishEntryDefinition definition, params AbsolutePath[] assets)
        where TPublishEntryDefinition : IPublishEntryDefinition
    {
        var value = definition.ReleaseAsset;
        value.Add(_ => Task.Run(() => assets));
        definition.ReleaseAsset = value;
        return definition;
    }

    /// <summary>
    /// Adds release assets to the publish entry definition using a function.
    /// </summary>
    /// <typeparam name="TPublishEntryDefinition">The type of the publish entry definition.</typeparam>
    /// <param name="definition">The publish entry definition.</param>
    /// <param name="assets">A function that returns the release assets.</param>
    /// <returns>The modified publish entry definition.</returns>
    public static TPublishEntryDefinition ReleaseAsset<TPublishEntryDefinition>(this TPublishEntryDefinition definition, Func<AbsolutePath[]> assets)
        where TPublishEntryDefinition : IPublishEntryDefinition
    {
        var value = definition.ReleaseAsset;
        value.Add(_ => Task.Run(() => assets()));
        definition.ReleaseAsset = value;
        return definition;
    }

    /// <summary>
    /// Adds release assets to the publish entry definition using a function that takes a run context.
    /// </summary>
    /// <typeparam name="TPublishEntryDefinition">The type of the publish entry definition.</typeparam>
    /// <param name="definition">The publish entry definition.</param>
    /// <param name="assets">A function that takes a run context and returns the release assets.</param>
    /// <returns>The modified publish entry definition.</returns>
    public static TPublishEntryDefinition ReleaseAsset<TPublishEntryDefinition>(this TPublishEntryDefinition definition, Func<IRunContext, AbsolutePath[]> assets)
        where TPublishEntryDefinition : IPublishEntryDefinition
    {
        var value = definition.ReleaseAsset;
        value.Add(runContext => Task.Run(() => assets(runContext)));
        definition.ReleaseAsset = value;
        return definition;
    }

    /// <summary>
    /// Adds release assets to the publish entry definition using an asynchronous function.
    /// </summary>
    /// <typeparam name="TPublishEntryDefinition">The type of the publish entry definition.</typeparam>
    /// <param name="definition">The publish entry definition.</param>
    /// <param name="assets">An asynchronous function that returns the release assets.</param>
    /// <returns>The modified publish entry definition.</returns>
    public static TPublishEntryDefinition ReleaseAsset<TPublishEntryDefinition>(this TPublishEntryDefinition definition, Func<Task<AbsolutePath[]>> assets)
        where TPublishEntryDefinition : IPublishEntryDefinition
    {
        var value = definition.ReleaseAsset;
        value.Add(_ => Task.Run(async () => await assets()));
        definition.ReleaseAsset = value;
        return definition;
    }

    /// <summary>
    /// Adds release assets to the publish entry definition using an asynchronous function that takes a run context.
    /// </summary>
    /// <typeparam name="TPublishEntryDefinition">The type of the publish entry definition.</typeparam>
    /// <param name="definition">The publish entry definition.</param>
    /// <param name="assets">An asynchronous function that takes a run context and returns the release assets.</param>
    /// <returns>The modified publish entry definition.</returns>
    public static TPublishEntryDefinition ReleaseAsset<TPublishEntryDefinition>(this TPublishEntryDefinition definition, Func<IRunContext, Task<AbsolutePath[]>> assets)
        where TPublishEntryDefinition : IPublishEntryDefinition
    {
        var value = definition.ReleaseAsset;
        value.Add(runContext => Task.Run(async () => await assets(runContext)));
        definition.ReleaseAsset = value;
        return definition;
    }

    /// <summary>
    /// Adds common release assets to the publish entry definition.
    /// </summary>
    /// <typeparam name="TPublishEntryDefinition">The type of the publish entry definition.</typeparam>
    /// <param name="definition">The publish entry definition.</param>
    /// <param name="assets">The common release assets.</param>
    /// <returns>The modified publish entry definition.</returns>
    public static TPublishEntryDefinition ReleaseCommonAsset<TPublishEntryDefinition>(this TPublishEntryDefinition definition, params AbsolutePath[] assets)
        where TPublishEntryDefinition : IPublishEntryDefinition
    {
        var value = definition.ReleaseCommonAsset;
        value.Add(_ => Task.Run(() => assets));
        definition.ReleaseCommonAsset = value;
        return definition;
    }

    /// <summary>
    /// Adds common release assets to the publish entry definition using a function.
    /// </summary>
    /// <typeparam name="TPublishEntryDefinition">The type of the publish entry definition.</typeparam>
    /// <param name="definition">The publish entry definition.</param>
    /// <param name="assets">A function that returns the common release assets.</param>
    /// <returns>The modified publish entry definition.</returns>
    public static TPublishEntryDefinition ReleaseCommonAsset<TPublishEntryDefinition>(this TPublishEntryDefinition definition, Func<AbsolutePath[]> assets)
        where TPublishEntryDefinition : IPublishEntryDefinition
    {
        var value = definition.ReleaseCommonAsset;
        value.Add(_ => Task.Run(() => assets()));
        definition.ReleaseCommonAsset = value;
        return definition;
    }

    /// <summary>
    /// Adds common release assets to the publish entry definition using a function that takes a run context.
    /// </summary>
    /// <typeparam name="TPublishEntryDefinition">The type of the publish entry definition.</typeparam>
    /// <param name="definition">The publish entry definition.</param>
    /// <param name="assets">A function that takes a run context and returns the common release assets.</param>
    /// <returns>The modified publish entry definition.</returns>
    public static TPublishEntryDefinition ReleaseCommonAsset<TPublishEntryDefinition>(this TPublishEntryDefinition definition, Func<IRunContext, AbsolutePath[]> assets)
        where TPublishEntryDefinition : IPublishEntryDefinition
    {
        var value = definition.ReleaseCommonAsset;
        value.Add(runContext => Task.Run(() => assets(runContext)));
        definition.ReleaseCommonAsset = value;
        return definition;
    }

    /// <summary>
    /// Adds common release assets to the publish entry definition using an asynchronous function.
    /// </summary>
    /// <typeparam name="TPublishEntryDefinition">The type of the publish entry definition.</typeparam>
    /// <param name="definition">The publish entry definition.</param>
    /// <param name="assets">An asynchronous function that returns the common release assets.</param>
    /// <returns>The modified publish entry definition.</returns>
    public static TPublishEntryDefinition ReleaseCommonAsset<TPublishEntryDefinition>(this TPublishEntryDefinition definition, Func<Task<AbsolutePath[]>> assets)
        where TPublishEntryDefinition : IPublishEntryDefinition
    {
        var value = definition.ReleaseCommonAsset;
        value.Add(_ => Task.Run(async () => await assets()));
        definition.ReleaseCommonAsset = value;
        return definition;
    }

    /// <summary>
    /// Adds common release assets to the publish entry definition using an asynchronous function that takes a run context.
    /// </summary>
    /// <typeparam name="TPublishEntryDefinition">The type of the publish entry definition.</typeparam>
    /// <param name="definition">The publish entry definition.</param>
    /// <param name="assets">An asynchronous function that takes a run context and returns the common release assets.</param>
    /// <returns>The modified publish entry definition.</returns>
    public static TPublishEntryDefinition ReleaseCommonAsset<TPublishEntryDefinition>(this TPublishEntryDefinition definition, Func<IRunContext, Task<AbsolutePath[]>> assets)
        where TPublishEntryDefinition : IPublishEntryDefinition
    {
        var value = definition.ReleaseCommonAsset;
        value.Add(runContext => Task.Run(async () => await assets(runContext)));
        definition.ReleaseCommonAsset = value;
        return definition;
    }
}
