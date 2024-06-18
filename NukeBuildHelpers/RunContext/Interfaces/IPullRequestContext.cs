using NukeBuildHelpers.Entry.Models;
using Semver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.RunContext.Models;

public interface IPullRequestContext : IVersionedContext
{
    new PullRequestReleaseVersion AppVersion { get; }
}
