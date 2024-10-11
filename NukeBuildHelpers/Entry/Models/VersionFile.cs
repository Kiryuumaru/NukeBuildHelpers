using Semver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Entry.Models;

internal class VersionFile
{
    public required string AppId { get; init; }

    public required Dictionary<string, SemVersion> EnvironmentVersions { get; init; }
}
