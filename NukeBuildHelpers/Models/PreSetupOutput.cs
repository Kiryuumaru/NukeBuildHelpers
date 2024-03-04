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

public class PreSetupOutput
{
    public required bool HasRelease { get; init; }

    public required bool IsFirstRelease { get; init; }

    public required string BuildTag { get; init; }

    public required string LastBuildTag { get; init; }

    public required Dictionary<string, PreSetupOutputVersion> Releases { get; init; }
}
