using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Models.RunContext;

namespace NukeBuildHelpers;

public abstract class AppTestEntry : Entry
{
    public abstract RunsOnType RunsOn { get; }

    public virtual RunTestType RunTestOn { get; } = RunTestType.All;

    public virtual Type[] AppEntryTargets { get; } = [];

    public virtual void Run(AppTestRunContext appTestContext) { }

    internal AppTestRunContext? AppTestContext { get; set; }
}

public abstract class AppTestEntry<TBuild> : AppTestEntry
    where TBuild : BaseNukeBuildHelpers
{
    public new TBuild NukeBuild => (TBuild)base.NukeBuild;
}
