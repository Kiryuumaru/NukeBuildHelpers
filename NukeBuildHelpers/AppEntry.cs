using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Models;

namespace NukeBuildHelpers;

/// <summary>
/// Represents an application entry with build and publish operations.
/// </summary>
public abstract class AppEntry : Entry
{
    /// <summary>
    /// Gets the operating system for the build runner.
    /// </summary>
    public abstract RunnerOS BuildRunnerOS { get; }

    /// <summary>
    /// Gets the operating system for the publish runner.
    /// </summary>
    public abstract RunnerOS PublishRunnerOS { get; }

    /// <summary>
    /// Gets the type of run for the build operation.
    /// </summary>
    public virtual RunType RunBuildOn { get; } = RunType.Bump;

    /// <summary>
    /// Gets the type of run for the publish operation.
    /// </summary>
    public virtual RunType RunPublishOn { get; } = RunType.Bump;

    /// <summary>
    /// Gets a value indicating whether this is a main release.
    /// </summary>
    public virtual bool MainRelease { get; } = false;

    /// <summary>
    /// Builds the application.
    /// </summary>
    /// <param name="appRunContext">The application run context.</param>
    public virtual void Build(AppRunContext appRunContext) { }

    /// <summary>
    /// Asynchronously builds the application.
    /// </summary>
    /// <param name="appRunContext">The application run context.</param>
    /// <returns>A task that represents the asynchronous build operation.</returns>
    public virtual Task BuildAsync(AppRunContext appRunContext) { return Task.CompletedTask; }

    /// <summary>
    /// Publishes the application.
    /// </summary>
    /// <param name="appRunContext">The application run context.</param>
    public virtual void Publish(AppRunContext appRunContext) { }

    /// <summary>
    /// Asynchronously publishes the application.
    /// </summary>
    /// <param name="appRunContext">The application run context.</param>
    /// <returns>A task that represents the asynchronous publish operation.</returns>
    public virtual Task PublishAsync(AppRunContext appRunContext) { return Task.CompletedTask; }

    internal AppRunContext? AppRunContext { get; set; }
}

/// <summary>
/// Represents an application entry with a specific type of Nuke build helpers.
/// </summary>
/// <typeparam name="TBuild">The type of the Nuke build helpers.</typeparam>
public abstract class AppEntry<TBuild> : AppEntry
    where TBuild : BaseNukeBuildHelpers
{
    /// <summary>
    /// Gets the Nuke build helpers instance.
    /// </summary>
    public new TBuild NukeBuild => (TBuild)base.NukeBuild;
}
