using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Models;

namespace NukeBuildHelpers;

public abstract class AppTestEntry : Entry
{
    public abstract RunnerOS RunnerOS { get; }

    public virtual RunTestType RunTestOn { get; } = RunTestType.All;

    public virtual Type[] AppEntryTargets { get; } = [];

    public virtual void Run(AppTestRunContext appTestContext) { }

    public virtual Task RunAsync(AppTestRunContext appTestContext) { return Task.CompletedTask; }

    internal AppTestRunContext? AppTestContext { get; set; }
}

public abstract class AppTestEntry<TBuild> : AppTestEntry
    where TBuild : BaseNukeBuildHelpers
{
    public new TBuild NukeBuild => (TBuild)base.NukeBuild;
}
