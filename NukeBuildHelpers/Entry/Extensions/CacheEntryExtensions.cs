using Nuke.Common.IO;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;
using NukeBuildHelpers.Runner.Abstraction;

namespace NukeBuildHelpers.Entry.Extensions;

/// <summary>
/// Extension methods for <see cref="IEntryDefinition"/> to configure various aspects of the cache entry.
/// </summary>
public static class CacheEntryExtensions
{
    /// <summary>
    /// Sets the cache paths for the entry definition.
    /// </summary>
    /// <typeparam name="TEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="cachePath">The cache paths to set.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TEntryDefinition CachePath<TEntryDefinition>(this TEntryDefinition definition, params AbsolutePath[] cachePath)
        where TEntryDefinition : IEntryDefinition
    {
        definition.CachePath.Add(_ => Task.Run(() => cachePath));
        return definition;
    }

    /// <summary>
    /// Sets the cache paths for the entry definition using a function that returns the paths.
    /// </summary>
    /// <typeparam name="TEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="cachePaths">The function that returns the cache paths.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TEntryDefinition CachePaths<TEntryDefinition>(this TEntryDefinition definition, Func<AbsolutePath[]> cachePaths)
        where TEntryDefinition : IEntryDefinition
    {
        definition.CachePath.Add(_ => Task.Run(() => cachePaths()));
        return definition;
    }

    /// <summary>
    /// Sets the cache paths for the entry definition using a function that takes a run context and returns the paths.
    /// </summary>
    /// <typeparam name="TEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="cachePaths">The function that takes a run context and returns the cache paths.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TEntryDefinition CachePaths<TEntryDefinition>(this TEntryDefinition definition, Func<IRunContext, AbsolutePath[]> cachePaths)
        where TEntryDefinition : IEntryDefinition
    {
        definition.CachePath.Add(runContext => Task.Run(() => cachePaths(runContext)));
        return definition;
    }

    /// <summary>
    /// Sets the cache paths for the entry definition using an asynchronous function that returns the paths.
    /// </summary>
    /// <typeparam name="TEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="cachePaths">The asynchronous function that returns the cache paths.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TEntryDefinition CachePaths<TEntryDefinition>(this TEntryDefinition definition, Func<Task<AbsolutePath[]>> cachePaths)
        where TEntryDefinition : IEntryDefinition
    {
        definition.CachePath.Add(_ => Task.Run(async () => await cachePaths()));
        return definition;
    }

    /// <summary>
    /// Sets the cache paths for the entry definition using an asynchronous function that takes a run context and returns the paths.
    /// </summary>
    /// <typeparam name="TEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="cachePaths">The asynchronous function that takes a run context and returns the cache paths.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TEntryDefinition CachePaths<TEntryDefinition>(this TEntryDefinition definition, Func<IRunContext, Task<AbsolutePath[]>> cachePaths)
        where TEntryDefinition : IEntryDefinition
    {
        definition.CachePath.Add(runContext => Task.Run(async () => await cachePaths(runContext)));
        return definition;
    }

    /// <summary>
    /// Sets the cache invalidator for the entry definition.
    /// </summary>
    /// <typeparam name="TEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="cacheInvalidator">The cache invalidator to set.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TEntryDefinition CacheInvalidator<TEntryDefinition>(this TEntryDefinition definition, string cacheInvalidator)
        where TEntryDefinition : IEntryDefinition
    {
        definition.CacheInvalidator = _ => Task.Run(() => cacheInvalidator);
        return definition;
    }

    /// <summary>
    /// Sets the cache invalidator for the entry definition using a function that returns the invalidator.
    /// </summary>
    /// <typeparam name="TEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="cacheInvalidator">The function that returns the cache invalidator.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TEntryDefinition CacheInvalidator<TEntryDefinition>(this TEntryDefinition definition, Func<string> cacheInvalidator)
        where TEntryDefinition : IEntryDefinition
    {
        definition.CacheInvalidator = _ => Task.Run(() => cacheInvalidator());
        return definition;
    }

    /// <summary>
    /// Sets the cache invalidator for the entry definition using a function that takes a run context and returns the invalidator.
    /// </summary>
    /// <typeparam name="TEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="cacheInvalidator">The function that takes a run context and returns the cache invalidator.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TEntryDefinition CacheInvalidator<TEntryDefinition>(this TEntryDefinition definition, Func<IRunContext, string> cacheInvalidator)
        where TEntryDefinition : IEntryDefinition
    {
        definition.CacheInvalidator = runContext => Task.Run(() => cacheInvalidator(runContext));
        return definition;
    }

    /// <summary>
    /// Sets the cache invalidator for the entry definition using an asynchronous function that returns the invalidator.
    /// </summary>
    /// <typeparam name="TEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="cacheInvalidator">The asynchronous function that returns the cache invalidator.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TEntryDefinition CacheInvalidator<TEntryDefinition>(this TEntryDefinition definition, Func<Task<string>> cacheInvalidator)
        where TEntryDefinition : IEntryDefinition
    {
        definition.CacheInvalidator = _ => Task.Run(async () => await cacheInvalidator());
        return definition;
    }

    /// <summary>
    /// Sets the cache invalidator for the entry definition using an asynchronous function that takes a run context and returns the invalidator.
    /// </summary>
    /// <typeparam name="TEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="cacheInvalidator">The asynchronous function that takes a run context and returns the cache invalidator.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TEntryDefinition CacheInvalidator<TEntryDefinition>(this TEntryDefinition definition, Func<IRunContext, Task<string>> cacheInvalidator)
        where TEntryDefinition : IEntryDefinition
    {
        definition.CacheInvalidator = runContext => Task.Run(async () => await cacheInvalidator(runContext));
        return definition;
    }
}
