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

internal class PullRequestContext : VersionedContext<PullRequestReleaseVersion>, IPullRequestContext
{
    PullRequestReleaseVersion IPullRequestContext.AppVersion { get => AppVersion; }
}
