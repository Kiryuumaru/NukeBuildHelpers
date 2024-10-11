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

    /// <summary>
    /// Configures whether to append release notes asset hashes in the workflow.
    /// </summary>
    /// <typeparam name="TWorkflowConfigEntryDefinition">The type of the workflow entry definition.</typeparam>
    /// <param name="definition">The instance of the entry definition.</param>
    /// <param name="appendReleaseNotesAssetHashes">A value indicating whether to append release notes asset hashes.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TWorkflowConfigEntryDefinition AppendReleaseNotesAssetHashes<TWorkflowConfigEntryDefinition>(this TWorkflowConfigEntryDefinition definition, bool appendReleaseNotesAssetHashes)
        where TWorkflowConfigEntryDefinition : IWorkflowConfigEntryDefinition
    {
        definition.AppendReleaseNotesAssetHashes = () => Task.Run(() => appendReleaseNotesAssetHashes);
        return definition;
    }

    /// <summary>
    /// Configures whether to append release notes asset hashes in the workflow using a function.
    /// </summary>
    /// <typeparam name="TWorkflowConfigEntryDefinition">The type of the workflow entry definition.</typeparam>
    /// <param name="definition">The instance of the entry definition.</param>
    /// <param name="appendReleaseNotesAssetHashes">A function that returns a value indicating whether to append release notes asset hashes.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TWorkflowConfigEntryDefinition AppendReleaseNotesAssetHashes<TWorkflowConfigEntryDefinition>(this TWorkflowConfigEntryDefinition definition, Func<bool> appendReleaseNotesAssetHashes)
        where TWorkflowConfigEntryDefinition : IWorkflowConfigEntryDefinition
    {
        definition.AppendReleaseNotesAssetHashes = () => Task.Run(() => appendReleaseNotesAssetHashes());
        return definition;
    }

    /// <summary>
    /// Configures whether to append release notes asset hashes in the workflow using an asynchronous function.
    /// </summary>
    /// <typeparam name="TWorkflowConfigEntryDefinition">The type of the workflow entry definition.</typeparam>
    /// <param name="definition">The instance of the entry definition.</param>
    /// <param name="appendReleaseNotesAssetHashes">An asynchronous function that returns a value indicating whether to append release notes asset hashes.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TWorkflowConfigEntryDefinition AppendReleaseNotesAssetHashes<TWorkflowConfigEntryDefinition>(this TWorkflowConfigEntryDefinition definition, Func<Task<bool>> appendReleaseNotesAssetHashes)
        where TWorkflowConfigEntryDefinition : IWorkflowConfigEntryDefinition
    {
        definition.AppendReleaseNotesAssetHashes = () => Task.Run(async () => await appendReleaseNotesAssetHashes());
        return definition;
    }

    /// <summary>
    /// Configures whether to enable prerelease on GitHub release if the environment is not on the main branch.
    /// </summary>
    /// <typeparam name="TWorkflowConfigEntryDefinition">The type of the workflow entry definition.</typeparam>
    /// <param name="definition">The instance of the entry definition.</param>
    /// <param name="enablePrereleaseOnRelease">A value indicating whether to enable prerelease on releases.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TWorkflowConfigEntryDefinition EnablePrereleaseOnRelease<TWorkflowConfigEntryDefinition>(this TWorkflowConfigEntryDefinition definition, bool enablePrereleaseOnRelease)
        where TWorkflowConfigEntryDefinition : IWorkflowConfigEntryDefinition
    {
        definition.EnablePrereleaseOnRelease = () => Task.Run(() => enablePrereleaseOnRelease);
        return definition;
    }

    /// <summary>
    /// Configures whether to enable prerelease on GitHub release if the environment is not on the main branch using a function.
    /// </summary>
    /// <typeparam name="TWorkflowConfigEntryDefinition">The type of the workflow entry definition.</typeparam>
    /// <param name="definition">The instance of the entry definition.</param>
    /// <param name="enablePrereleaseOnRelease">A function that returns a value indicating whether to enable prerelease on releases.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TWorkflowConfigEntryDefinition EnablePrereleaseOnRelease<TWorkflowConfigEntryDefinition>(this TWorkflowConfigEntryDefinition definition, Func<bool> enablePrereleaseOnRelease)
        where TWorkflowConfigEntryDefinition : IWorkflowConfigEntryDefinition
    {
        definition.EnablePrereleaseOnRelease = () => Task.Run(() => enablePrereleaseOnRelease());
        return definition;
    }

    /// <summary>
    /// Configures whether to enable prerelease on GitHub release if the environment is not on the main branch using an asynchronous function.
    /// </summary>
    /// <typeparam name="TWorkflowConfigEntryDefinition">The type of the workflow entry definition.</typeparam>
    /// <param name="definition">The instance of the entry definition.</param>
    /// <param name="enablePrereleaseOnRelease">An asynchronous function that returns a value indicating whether to enable prerelease on releases.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TWorkflowConfigEntryDefinition EnablePrereleaseOnRelease<TWorkflowConfigEntryDefinition>(this TWorkflowConfigEntryDefinition definition, Func<Task<bool>> enablePrereleaseOnRelease)
        where TWorkflowConfigEntryDefinition : IWorkflowConfigEntryDefinition
    {
        definition.EnablePrereleaseOnRelease = () => Task.Run(async () => await enablePrereleaseOnRelease());
        return definition;
    }

    /// <summary>
    /// Sets the starting build ID for the workflow.
    /// </summary>
    /// <typeparam name="TWorkflowConfigEntryDefinition">The type of the workflow entry definition.</typeparam>
    /// <param name="definition">The workflow entry definition instance.</param>
    /// <param name="startingBuildId">The starting build ID to set.</param>
    /// <returns>The modified workflow entry definition instance.</returns>
    public static TWorkflowConfigEntryDefinition StartingBuildId<TWorkflowConfigEntryDefinition>(this TWorkflowConfigEntryDefinition definition, long startingBuildId)
        where TWorkflowConfigEntryDefinition : IWorkflowConfigEntryDefinition
    {
        definition.StartingBuildId = () => Task.Run(() => startingBuildId);
        return definition;
    }

    /// <summary>
    /// Sets the starting build ID using a function for the workflow.
    /// </summary>
    /// <typeparam name="TWorkflowConfigEntryDefinition">The type of the workflow entry definition.</typeparam>
    /// <param name="definition">The workflow entry definition instance.</param>
    /// <param name="startingBuildId">A function returning the starting build ID.</param>
    /// <returns>The modified workflow entry definition instance.</returns>
    public static TWorkflowConfigEntryDefinition StartingBuildId<TWorkflowConfigEntryDefinition>(this TWorkflowConfigEntryDefinition definition, Func<long> startingBuildId)
        where TWorkflowConfigEntryDefinition : IWorkflowConfigEntryDefinition
    {
        definition.StartingBuildId = () => Task.Run(() => startingBuildId());
        return definition;
    }

    /// <summary>
    /// Sets the starting build ID using an asynchronous function for the workflow.
    /// </summary>
    /// <typeparam name="TWorkflowConfigEntryDefinition">The type of the workflow entry definition.</typeparam>
    /// <param name="definition">The workflow entry definition instance.</param>
    /// <param name="startingBuildId">An asynchronous function returning the starting build ID.</param>
    /// <returns>The modified workflow entry definition instance.</returns>
    public static TWorkflowConfigEntryDefinition StartingBuildId<TWorkflowConfigEntryDefinition>(this TWorkflowConfigEntryDefinition definition, Func<Task<long>> startingBuildId)
        where TWorkflowConfigEntryDefinition : IWorkflowConfigEntryDefinition
    {
        definition.StartingBuildId = () => Task.Run(async () => await startingBuildId());
        return definition;
    }

    /// <summary>
    /// Configures whether to use JSON file versioning in the workflow. If enabled, a file named <c>versions.json</c> will be generated in the <see cref="Nuke.Common.NukeBuild.RootDirectory"/> on every build workflow generation.
    /// </summary>
    /// <typeparam name="TWorkflowConfigEntryDefinition">The type of the workflow entry definition.</typeparam>
    /// <param name="definition">The instance of the entry definition.</param>
    /// <param name="useJsonFileVersioning">A value indicating whether to use JSON file versioning.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TWorkflowConfigEntryDefinition UseJsonFileVersioning<TWorkflowConfigEntryDefinition>(this TWorkflowConfigEntryDefinition definition, bool useJsonFileVersioning)
        where TWorkflowConfigEntryDefinition : IWorkflowConfigEntryDefinition
    {
        definition.UseJsonFileVersioning = () => Task.Run(() => useJsonFileVersioning);
        return definition;
    }

    /// <summary>
    /// Configures whether to use JSON file versioning in the workflow using a function. If enabled, a file named <c>versions.json</c> will be generated in the <see cref="Nuke.Common.NukeBuild.RootDirectory"/> on every build workflow generation.
    /// </summary>
    /// <typeparam name="TWorkflowConfigEntryDefinition">The type of the workflow entry definition.</typeparam>
    /// <param name="definition">The instance of the entry definition.</param>
    /// <param name="useJsonFileVersioning">A function that returns a value indicating whether to use JSON file versioning.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TWorkflowConfigEntryDefinition UseJsonFileVersioning<TWorkflowConfigEntryDefinition>(this TWorkflowConfigEntryDefinition definition, Func<bool> useJsonFileVersioning)
        where TWorkflowConfigEntryDefinition : IWorkflowConfigEntryDefinition
    {
        definition.UseJsonFileVersioning = () => Task.Run(() => useJsonFileVersioning());
        return definition;
    }

    /// <summary>
    /// Configures whether to use JSON file versioning in the workflow using an asynchronous function. If enabled, a file named <c>versions.json</c> will be generated in the <see cref="Nuke.Common.NukeBuild.RootDirectory"/> on every build workflow generation.
    /// </summary>
    /// <typeparam name="TWorkflowConfigEntryDefinition">The type of the workflow entry definition.</typeparam>
    /// <param name="definition">The instance of the entry definition.</param>
    /// <param name="useJsonFileVersioning">An asynchronous function that returns a value indicating whether to use JSON file versioning.</param>
    /// <returns>The modified entry definition instance.</returns>
    public static TWorkflowConfigEntryDefinition UseJsonFileVersioning<TWorkflowConfigEntryDefinition>(this TWorkflowConfigEntryDefinition definition, Func<Task<bool>> useJsonFileVersioning)
        where TWorkflowConfigEntryDefinition : IWorkflowConfigEntryDefinition
    {
        definition.UseJsonFileVersioning = () => Task.Run(async () => await useJsonFileVersioning());
        return definition;
    }
}
