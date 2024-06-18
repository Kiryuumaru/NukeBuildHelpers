using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.Pipelines.Common.Enums;
using NukeBuildHelpers.RunContext.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
