using Nuke.Common.IO;
using Semver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Models;

internal class AllVersions
{
    public required Dictionary<string, List<long>> CommitBuildIdGrouped { get; init; }

    public required Dictionary<string, List<SemVersion>> CommitVersionGrouped { get; init; }

    public required Dictionary<string, List<string>> CommitLatestTagGrouped { get; init; }

    public required Dictionary<SemVersion, string> VersionCommitPaired { get; init; }

    public required Dictionary<long, string> BuildIdCommitPaired { get; init; }

    public required Dictionary<string, List<SemVersion>> VersionEnvGrouped { get; init; }

    public required Dictionary<string, SemVersion> EnvLatestVersionPaired { get; init; }

    public required Dictionary<string, long> EnvLatestBuildIdPaired { get; init; }

    public required List<string> EnvSorted { get; init; }
}
