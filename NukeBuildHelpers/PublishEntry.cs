using Microsoft.Build.Tasks;
using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Models;

namespace NukeBuildHelpers;

public abstract class PublishEntry : Entry2<AppRunContext>
{
    public virtual RunType RunOn { get; } = RunType.Bump;
}

public abstract class PublishEntry<TBuild> : PublishEntry
    where TBuild : BaseNukeBuildHelpers
{
    public new TBuild NukeBuild => (TBuild)base.NukeBuild;
}
