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
    public List<SemVersion> VersionList { get; init; }

    public Dictionary<string, List<SemVersion>> VersionGrouped { get; init; }

    public List<string> GroupKeySorted { get; init; }
}
