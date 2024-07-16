using Nuke.Common.IO;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;
using NukeBuildHelpers.Runner.Abstraction;

namespace NukeBuildHelpers.Entry.Extensions;

/// <summary>
/// Extension methods for <see cref="IRunEntryDefinition"/> to configure various aspects of the cache entry.
/// </summary>
public static class CacheEntryExtensions
{
    /// <summary>
    /// Sets the cache paths for the entry definition.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="cachePath">The cache paths to set.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TRunEntryDefinition CachePath<TRunEntryDefinition>(this TRunEntryDefinition definition, params AbsolutePath[] cachePath)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.CachePath.Add(_ => Task.Run(() => cachePath));
        return definition;
    }

    /// <summary>
    /// Sets the cache paths for the entry definition using a function that returns the paths.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="cachePaths">The function that returns the cache paths.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TRunEntryDefinition CachePath<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<AbsolutePath[]> cachePaths)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.CachePath.Add(_ => Task.Run(() => cachePaths()));
        return definition;
    }

    /// <summary>
    /// Sets the cache paths for the entry definition using a function that takes a run context and returns the paths.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="cachePaths">The function that takes a run context and returns the cache paths.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TRunEntryDefinition CachePath<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<IRunContext, AbsolutePath[]> cachePaths)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.CachePath.Add(runContext => Task.Run(() => cachePaths(runContext)));
        return definition;
    }

    /// <summary>
    /// Sets the cache paths for the entry definition using an asynchronous function that returns the paths.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="cachePaths">The asynchronous function that returns the cache paths.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TRunEntryDefinition CachePath<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<Task<AbsolutePath[]>> cachePaths)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.CachePath.Add(_ => Task.Run(async () => await cachePaths()));
        return definition;
    }

    /// <summary>
    /// Sets the cache paths for the entry definition using an asynchronous function that takes a run context and returns the paths.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="cachePaths">The asynchronous function that takes a run context and returns the cache paths.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TRunEntryDefinition CachePath<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<IRunContext, Task<AbsolutePath[]>> cachePaths)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.CachePath.Add(runContext => Task.Run(async () => await cachePaths(runContext)));
        return definition;
    }

    /// <summary>
    /// Sets the cache invalidator for the entry definition.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="cacheInvalidator">The cache invalidator to set.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TRunEntryDefinition CacheInvalidator<TRunEntryDefinition>(this TRunEntryDefinition definition, string cacheInvalidator)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.CacheInvalidator = _ => Task.Run(() => cacheInvalidator);
        return definition;
    }

    /// <summary>
    /// Sets the cache invalidator for the entry definition using a function that returns the invalidator.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="cacheInvalidator">The function that returns the cache invalidator.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TRunEntryDefinition CacheInvalidator<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<string> cacheInvalidator)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.CacheInvalidator = _ => Task.Run(() => cacheInvalidator());
        return definition;
    }

    /// <summary>
    /// Sets the cache invalidator for the entry definition using a function that takes a run context and returns the invalidator.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="cacheInvalidator">The function that takes a run context and returns the cache invalidator.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TRunEntryDefinition CacheInvalidator<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<IRunContext, string> cacheInvalidator)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.CacheInvalidator = runContext => Task.Run(() => cacheInvalidator(runContext));
        return definition;
    }

    /// <summary>
    /// Sets the cache invalidator for the entry definition using an asynchronous function that returns the invalidator.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="cacheInvalidator">The asynchronous function that returns the cache invalidator.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TRunEntryDefinition CacheInvalidator<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<Task<string>> cacheInvalidator)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.CacheInvalidator = _ => Task.Run(async () => await cacheInvalidator());
        return definition;
    }

    /// <summary>
    /// Sets the cache invalidator for the entry definition using an asynchronous function that takes a run context and returns the invalidator.
    /// </summary>
    /// <typeparam name="TRunEntryDefinition">The type of the entry definition.</typeparam>
    /// <param name="definition">The entry definition instance.</param>
    /// <param name="cacheInvalidator">The asynchronous function that takes a run context and returns the cache invalidator.</param>
    /// <returns>The updated entry definition instance.</returns>
    public static TRunEntryDefinition CacheInvalidator<TRunEntryDefinition>(this TRunEntryDefinition definition, Func<IRunContext, Task<string>> cacheInvalidator)
        where TRunEntryDefinition : IRunEntryDefinition
    {
        definition.CacheInvalidator = runContext => Task.Run(async () => await cacheInvalidator(runContext));
        return definition;
    }
}
