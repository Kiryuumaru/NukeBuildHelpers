using Microsoft.Build.Tasks;
using Nuke.Common;
using Nuke.Common.IO;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Enums;
using Semver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace NukeBuildHelpers;

internal class PreSetupOutput
{
    public required string Branch { get; init; }

    public required TriggerType TriggerType { get; init; }

    public required bool HasRelease { get; init; }

    public required bool HasEntries { get; init; }

    public required bool HasTest { get; init; }

    public required bool HasBuild { get; init; }

    public required bool HasPublish { get; init; }

    public required string ReleaseNotes { get; init; }

    public required bool IsFirstRelease { get; init; }

    public required long BuildId { get; init; }

    public required long LastBuildId { get; init; }

    public required string Environment { get; init; }

    public required Dictionary<string, PreSetupOutputVersion> Entries { get; init; }

    public required List<string> ToTest { get; init; }

    public required List<string> ToBuild { get; init; }

    public required List<string> ToPublish { get; init; }
}
