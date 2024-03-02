using Nuke.Common.IO;
using Semver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Models;

public class AllVersions
{
    public required List<SemVersion> VersionList { get; init; }

    public required Dictionary<string, List<SemVersion>> VersionGrouped { get; init; }

    public required Dictionary<string, SemVersion> LatestVersions { get; init; }

    public required List<string> GroupKeySorted { get; init; }
}
