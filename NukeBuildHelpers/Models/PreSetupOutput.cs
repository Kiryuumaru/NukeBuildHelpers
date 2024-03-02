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

public class PreSetupOutputVersion
{
    public required string AppId { get; init; }

    public required string Environment { get; init; }

    public required string Version { get; init; }
}

public class PreSetupOutput
{
    public bool HasRelease { get; init; }

    public required Dictionary<string, PreSetupOutputVersion> Releases { get; init; }
}
