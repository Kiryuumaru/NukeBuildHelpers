using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Models.RunContext;

namespace NukeBuildHelpers;

public abstract class AppEntry : Entry
{
    public abstract RunsOnType BuildRunsOn { get; }

    public abstract RunsOnType PublishRunsOn { get; }

    public virtual RunType RunBuildOn { get; } = RunType.Bump;

    public virtual RunType RunPublishOn { get; } = RunType.Bump;

    public virtual bool MainRelease { get; } = true;

    public virtual void Build(AppRunContext appRunContext) { }

    public virtual void Publish(AppRunContext appRunContext) { }

    internal AppRunContext? AppRunContext { get; set; }
}

public abstract class AppEntry<TBuild> : AppEntry
    where TBuild : BaseNukeBuildHelpers
{
    public new TBuild NukeBuild => (TBuild)base.NukeBuild;
}
