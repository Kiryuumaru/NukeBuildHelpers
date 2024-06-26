using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.RunContext.Models;

namespace NukeBuildHelpers.RunContext.Interfaces;

internal class VersionedContext<TAppVersion> : CommitContext, IVersionedContext
    where TAppVersion : AppVersion
{
    public required TAppVersion AppVersion { get; init; }

    AppVersion IVersionedContext.AppVersion { get => AppVersion; }
}

internal class VersionedContext : VersionedContext<AppVersion>
{
}
