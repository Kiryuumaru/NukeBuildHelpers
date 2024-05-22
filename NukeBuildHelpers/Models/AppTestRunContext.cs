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

public class AppTestRunContext : RunContext
{
    internal AppTestRunContext(AbsolutePath outputDirectory, RunType runType)
        : base(outputDirectory, runType)
    {
    }
}
