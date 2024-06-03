namespace NukeBuildHelpers;

public abstract class WorkflowStep : BaseHelper
{
    public virtual int Priority { get; set; } = 0;

    public virtual void TestRun(AppTestEntry appTestEntry) { }

    public virtual void AppBuild(AppEntry appEntry) { }

    public virtual Task AppBuildAsync(AppEntry appEntry) { return Task.CompletedTask; }

    public virtual void AppPublish(AppEntry appEntry) { }

    public virtual Task AppPublishAsync(AppEntry appEntry) { return Task.CompletedTask; }
}

public abstract class WorkflowStep<TBuild> : WorkflowStep
    where TBuild : BaseNukeBuildHelpers
{
    public new TBuild NukeBuild => (TBuild)base.NukeBuild;
}
