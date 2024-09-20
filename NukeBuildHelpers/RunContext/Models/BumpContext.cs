using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.RunContext.Interfaces;

namespace NukeBuildHelpers.RunContext.Models;

internal class BumpContext : VersionedContext<BumpReleaseVersion>, IBumpContext
{
    BumpReleaseVersion IBumpContext.AppVersion { get => AppVersion; }
}
