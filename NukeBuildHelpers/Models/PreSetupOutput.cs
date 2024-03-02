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
    public string Environment { get; set; }

    public string Version { get; set; }
}

public class PreSetupOutput
{
    public bool HasRelease { get; set; }

    public Dictionary<string, PreSetupOutputVersion> Releases { get; set; }
}
