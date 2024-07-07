using Nuke.Common.IO;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;

namespace NukeBuildHelpers.Entry.Extensions;

/// <summary>
/// Extension methods for <see cref="IBuildEntryDefinition"/>.
/// </summary>
public static class BuildEntryExtensions
{
    /// <summary>
    /// Adds release assets to the build entry definition.
    /// </summary>
    /// <typeparam name="TBuildEntryDefinition">The type of the build entry definition.</typeparam>
    /// <param name="definition">The build entry definition.</param>
    /// <param name="assets">The release assets.</param>
    /// <returns>The modified build entry definition.</returns>
    public static TBuildEntryDefinition ReleaseAsset<TBuildEntryDefinition>(this TBuildEntryDefinition definition, params AbsolutePath[] assets)
        where TBuildEntryDefinition : IBuildEntryDefinition
    {
        definition.ReleaseAsset.Add(_ => Task.Run(() => assets));
        return definition;
    }

    /// <summary>
    /// Adds release assets to the build entry definition using a function.
    /// </summary>
    /// <typeparam name="TBuildEntryDefinition">The type of the build entry definition.</typeparam>
    /// <param name="definition">The build entry definition.</param>
    /// <param name="assets">A function that returns the release assets.</param>
    /// <returns>The modified build entry definition.</returns>
    public static TBuildEntryDefinition ReleaseAsset<TBuildEntryDefinition>(this TBuildEntryDefinition definition, Func<AbsolutePath[]> assets)
        where TBuildEntryDefinition : IBuildEntryDefinition
    {
        definition.ReleaseAsset.Add(_ => Task.Run(() => assets()));
        return definition;
    }

    /// <summary>
    /// Adds release assets to the build entry definition using a function that takes a run context.
    /// </summary>
    /// <typeparam name="TBuildEntryDefinition">The type of the build entry definition.</typeparam>
    /// <param name="definition">The build entry definition.</param>
    /// <param name="assets">A function that takes a run context and returns the release assets.</param>
    /// <returns>The modified build entry definition.</returns>
    public static TBuildEntryDefinition ReleaseAsset<TBuildEntryDefinition>(this TBuildEntryDefinition definition, Func<IRunContext, AbsolutePath[]> assets)
        where TBuildEntryDefinition : IBuildEntryDefinition
    {
        definition.ReleaseAsset.Add(runContext => Task.Run(() => assets(runContext)));
        return definition;
    }

    /// <summary>
    /// Adds release assets to the build entry definition using an asynchronous function.
    /// </summary>
    /// <typeparam name="TBuildEntryDefinition">The type of the build entry definition.</typeparam>
    /// <param name="definition">The build entry definition.</param>
    /// <param name="assets">An asynchronous function that returns the release assets.</param>
    /// <returns>The modified build entry definition.</returns>
    public static TBuildEntryDefinition ReleaseAsset<TBuildEntryDefinition>(this TBuildEntryDefinition definition, Func<Task<AbsolutePath[]>> assets)
        where TBuildEntryDefinition : IBuildEntryDefinition
    {
        definition.ReleaseAsset.Add(_ => Task.Run(async () => await assets()));
        return definition;
    }

    /// <summary>
    /// Adds release assets to the build entry definition using an asynchronous function that takes a run context.
    /// </summary>
    /// <typeparam name="TBuildEntryDefinition">The type of the build entry definition.</typeparam>
    /// <param name="definition">The build entry definition.</param>
    /// <param name="assets">An asynchronous function that takes a run context and returns the release assets.</param>
    /// <returns>The modified build entry definition.</returns>
    public static TBuildEntryDefinition ReleaseAsset<TBuildEntryDefinition>(this TBuildEntryDefinition definition, Func<IRunContext, Task<AbsolutePath[]>> assets)
        where TBuildEntryDefinition : IBuildEntryDefinition
    {
        definition.ReleaseAsset.Add(runContext => Task.Run(async () => await assets(runContext)));
        return definition;
    }

    /// <summary>
    /// Adds common release assets to the build entry definition.
    /// </summary>
    /// <typeparam name="TBuildEntryDefinition">The type of the build entry definition.</typeparam>
    /// <param name="definition">The build entry definition.</param>
    /// <param name="assets">The common release assets.</param>
    /// <returns>The modified build entry definition.</returns>
    public static TBuildEntryDefinition CommonReleaseAsset<TBuildEntryDefinition>(this TBuildEntryDefinition definition, params AbsolutePath[] assets)
        where TBuildEntryDefinition : IBuildEntryDefinition
    {
        definition.CommonReleaseAsset.Add(_ => Task.Run(() => assets));
        return definition;
    }

    /// <summary>
    /// Adds common release assets to the build entry definition using a function.
    /// </summary>
    /// <typeparam name="TBuildEntryDefinition">The type of the build entry definition.</typeparam>
    /// <param name="definition">The build entry definition.</param>
    /// <param name="assets">A function that returns the common release assets.</param>
    /// <returns>The modified build entry definition.</returns>
    public static TBuildEntryDefinition CommonReleaseAsset<TBuildEntryDefinition>(this TBuildEntryDefinition definition, Func<AbsolutePath[]> assets)
        where TBuildEntryDefinition : IBuildEntryDefinition
    {
        definition.CommonReleaseAsset.Add(_ => Task.Run(() => assets()));
        return definition;
    }

    /// <summary>
    /// Adds common release assets to the build entry definition using a function that takes a run context.
    /// </summary>
    /// <typeparam name="TBuildEntryDefinition">The type of the build entry definition.</typeparam>
    /// <param name="definition">The build entry definition.</param>
    /// <param name="assets">A function that takes a run context and returns the common release assets.</param>
    /// <returns>The modified build entry definition.</returns>
    public static TBuildEntryDefinition CommonReleaseAsset<TBuildEntryDefinition>(this TBuildEntryDefinition definition, Func<IRunContext, AbsolutePath[]> assets)
        where TBuildEntryDefinition : IBuildEntryDefinition
    {
        definition.CommonReleaseAsset.Add(runContext => Task.Run(() => assets(runContext)));
        return definition;
    }

    /// <summary>
    /// Adds common release assets to the build entry definition using an asynchronous function.
    /// </summary>
    /// <typeparam name="TBuildEntryDefinition">The type of the build entry definition.</typeparam>
    /// <param name="definition">The build entry definition.</param>
    /// <param name="assets">An asynchronous function that returns the common release assets.</param>
    /// <returns>The modified build entry definition.</returns>
    public static TBuildEntryDefinition CommonReleaseAsset<TBuildEntryDefinition>(this TBuildEntryDefinition definition, Func<Task<AbsolutePath[]>> assets)
        where TBuildEntryDefinition : IBuildEntryDefinition
    {
        definition.CommonReleaseAsset.Add(_ => Task.Run(async () => await assets()));
        return definition;
    }

    /// <summary>
    /// Adds common release assets to the build entry definition using an asynchronous function that takes a run context.
    /// </summary>
    /// <typeparam name="TBuildEntryDefinition">The type of the build entry definition.</typeparam>
    /// <param name="definition">The build entry definition.</param>
    /// <param name="assets">An asynchronous function that takes a run context and returns the common release assets.</param>
    /// <returns>The modified build entry definition.</returns>
    public static TBuildEntryDefinition CommonReleaseAsset<TBuildEntryDefinition>(this TBuildEntryDefinition definition, Func<IRunContext, Task<AbsolutePath[]>> assets)
        where TBuildEntryDefinition : IBuildEntryDefinition
    {
        definition.CommonReleaseAsset.Add(runContext => Task.Run(async () => await assets(runContext)));
        return definition;
    }
}
