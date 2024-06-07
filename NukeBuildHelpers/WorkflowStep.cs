namespace NukeBuildHelpers;

/// <summary>
/// Represents a workflow step with test, build, and publish operations.
/// </summary>
public abstract class WorkflowStep : BaseHelper
{
    /// <summary>
    /// Gets or sets the priority of the workflow step.
    /// </summary>
    public virtual int Priority { get; set; } = 0;

    /// <summary>
    /// Executes the test run step in the workflow.
    /// </summary>
    /// <param name="appTestEntry">The application test entry.</param>
    public virtual void TestRun(AppTestEntry appTestEntry) { }

    /// <summary>
    /// Executes the build step in the workflow.
    /// </summary>
    /// <param name="appEntry">The application entry.</param>
    public virtual void AppBuild(AppEntry appEntry) { }

    /// <summary>
    /// Asynchronously executes the build step in the workflow.
    /// </summary>
    /// <param name="appEntry">The application entry.</param>
    /// <returns>A task that represents the asynchronous build operation.</returns>
    public virtual Task AppBuildAsync(AppEntry appEntry) { return Task.CompletedTask; }

    /// <summary>
    /// Executes the publish step in the workflow.
    /// </summary>
    /// <param name="appEntry">The application entry.</param>
    public virtual void AppPublish(AppEntry appEntry) { }

    /// <summary>
    /// Asynchronously executes the publish step in the workflow.
    /// </summary>
    /// <param name="appEntry">The application entry.</param>
    /// <returns>A task that represents the asynchronous publish operation.</returns>
    public virtual Task AppPublishAsync(AppEntry appEntry) { return Task.CompletedTask; }
}

/// <summary>
/// Represents a workflow step with a specific type of Nuke build helpers.
/// </summary>
/// <typeparam name="TBuild">The type of the Nuke build helpers.</typeparam>
public abstract class WorkflowStep<TBuild> : WorkflowStep
    where TBuild : BaseNukeBuildHelpers
{
    /// <summary>
    /// Gets the Nuke build helpers instance.
    /// </summary>
    public new TBuild NukeBuild => (TBuild)base.NukeBuild;
}
