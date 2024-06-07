using Microsoft.Build.Tasks;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Models;

namespace NukeBuildHelpers;

public abstract class TestEntry : Entry2<AppTestRunContext>
{
    public override string Id { get => GetType().Name.ToSnakeCase(); }

    public virtual RunTestType RunOn { get; } = RunTestType.All;

    public abstract string[] TargetAppIds { get; }
}

public abstract class TestEntry<TBuild> : TestEntry
    where TBuild : BaseNukeBuildHelpers
{
    public new TBuild NukeBuild => (TBuild)base.NukeBuild;
}
