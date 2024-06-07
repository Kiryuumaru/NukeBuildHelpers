using Microsoft.Build.Tasks;
using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Models;

namespace NukeBuildHelpers;

public abstract class BuildEntry : Entry2<AppRunContext>
{
    public virtual RunType RunOn { get; } = RunType.Bump;
}

public abstract class BuildEntry<TBuild> : BuildEntry
    where TBuild : BaseNukeBuildHelpers
{
    public new TBuild NukeBuild => (TBuild)base.NukeBuild;
}
