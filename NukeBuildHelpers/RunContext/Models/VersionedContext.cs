using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.RunContext.Interfaces;

namespace NukeBuildHelpers.RunContext.Models;

internal class VersionedContext<TAppVersion> : CommitContext, IVersionedContext
    where TAppVersion : AppVersion
{
    public required TAppVersion AppVersion { get; init; }

    AppVersion IVersionedContext.AppVersion { get => AppVersion; }
}

internal class VersionedContext : VersionedContext<AppVersion>
{
}
