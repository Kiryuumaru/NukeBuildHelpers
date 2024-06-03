using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Models.RunContext;

namespace NukeBuildHelpers;

public abstract class AppEntry : Entry
{
    public abstract RunsOnType BuildRunsOn { get; }

    public abstract RunsOnType PublishRunsOn { get; }

    public virtual RunType RunBuildOn { get; } = RunType.Bump;

    public virtual RunType RunPublishOn { get; } = RunType.Bump;

    public virtual bool MainRelease { get; } = false;

    public virtual void Build(AppRunContext appRunContext) { }

    public virtual Task BuildAsync(AppRunContext appRunContext) { return Task.CompletedTask; }

    public virtual void Publish(AppRunContext appRunContext) { }

    public virtual Task PublishAsync(AppRunContext appRunContext) { return Task.CompletedTask; }

    internal AppRunContext? AppRunContext { get; set; }
}

public abstract class AppEntry<TBuild> : AppEntry
    where TBuild : BaseNukeBuildHelpers
{
    public new TBuild NukeBuild => (TBuild)base.NukeBuild;
}
