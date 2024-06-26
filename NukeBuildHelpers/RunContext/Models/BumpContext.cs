using NukeBuildHelpers.Entry.Models;

namespace NukeBuildHelpers.RunContext.Interfaces;

internal class BumpContext : VersionedContext<BumpReleaseVersion>, IBumpContext
{
    BumpReleaseVersion IBumpContext.AppVersion { get => AppVersion; }
}
