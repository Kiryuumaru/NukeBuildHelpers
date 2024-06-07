using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Models;

namespace NukeBuildHelpers;

/// <summary>
/// Represents a test entry with common properties and methods.
/// </summary>
public abstract class AppTestEntry : Entry
{
    /// <summary>
    /// Gets the operating system for the runner.
    /// </summary>
    public abstract RunnerOS RunnerOS { get; }

    /// <summary>
    /// Gets the type of run for the test operation.
    /// </summary>
    public virtual RunTestType RunTestOn { get; } = RunTestType.All;

    /// <summary>
    /// Gets the application entry targets.
    /// </summary>
    public virtual Type[] AppEntryTargets { get; } = [];

    /// <summary>
    /// Runs the test operation.
    /// </summary>
    /// <param name="appTestContext">The application test run context.</param>
    public virtual void Run(AppTestRunContext appTestContext) { }

    /// <summary>
    /// Asynchronously runs the test operation.
    /// </summary>
    /// <param name="appTestContext">The application test run context.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    public virtual Task RunAsync(AppTestRunContext appTestContext) { return Task.CompletedTask; }

    internal AppTestRunContext? AppTestContext { get; set; }
}

/// <summary>
/// Represents a test entry with a specific type of Nuke build helpers.
/// </summary>
/// <typeparam name="TBuild">The type of the Nuke build helpers.</typeparam>
public abstract class AppTestEntry<TBuild> : AppTestEntry
    where TBuild : BaseNukeBuildHelpers
{
    /// <summary>
    /// Gets the Nuke build helpers instance.
    /// </summary>
    public new TBuild NukeBuild => (TBuild)base.NukeBuild;
}
