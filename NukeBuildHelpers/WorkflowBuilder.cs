namespace NukeBuildHelpers;

/// <summary>
/// Represents a workflow builder with pre- and post- steps for test, build, and publish operations.
/// </summary>
public abstract class WorkflowBuilder : BaseHelper
{
    /// <summary>
    /// Gets or sets the priority of the workflow builder.
    /// </summary>
    public virtual int Priority { get; set; } = 0;

    /// <summary>
    /// Executes before the test run in the workflow.
    /// </summary>
    /// <param name="step">The step dictionary containing step details.</param>
    public virtual void WorkflowBuilderPreTestRun(Dictionary<string, object> step) { }

    /// <summary>
    /// Executes after the test run in the workflow.
    /// </summary>
    /// <param name="step">The step dictionary containing step details.</param>
    public virtual void WorkflowBuilderPostTestRun(Dictionary<string, object> step) { }

    /// <summary>
    /// Executes before the build run in the workflow.
    /// </summary>
    /// <param name="step">The step dictionary containing step details.</param>
    public virtual void WorkflowBuilderPreBuildRun(Dictionary<string, object> step) { }

    /// <summary>
    /// Executes after the build run in the workflow.
    /// </summary>
    /// <param name="step">The step dictionary containing step details.</param>
    public virtual void WorkflowBuilderPostBuildRun(Dictionary<string, object> step) { }

    /// <summary>
    /// Executes before the publish run in the workflow.
    /// </summary>
    /// <param name="step">The step dictionary containing step details.</param>
    public virtual void WorkflowBuilderPrePublishRun(Dictionary<string, object> step) { }

    /// <summary>
    /// Executes after the publish run in the workflow.
    /// </summary>
    /// <param name="step">The step dictionary containing step details.</param>
    public virtual void WorkflowBuilderPostPublishRun(Dictionary<string, object> step) { }
}

/// <summary>
/// Represents a workflow builder with a specific type of Nuke build helpers.
/// </summary>
/// <typeparam name="TBuild">The type of the Nuke build helpers.</typeparam>
public abstract class WorkflowBuilder<TBuild> : WorkflowBuilder
    where TBuild : BaseNukeBuildHelpers
{
    /// <summary>
    /// Gets the Nuke build helpers instance.
    /// </summary>
    public new TBuild NukeBuild => (TBuild)base.NukeBuild;
}
