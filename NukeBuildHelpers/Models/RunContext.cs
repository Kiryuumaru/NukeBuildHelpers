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

public class RunContext
{
    public AbsolutePath OutputDirectory { get; }

    public RunType RunType { get; }

    internal RunContext(AbsolutePath outputDirectory, RunType runType)
    {
        OutputDirectory = outputDirectory;
        RunType = runType;
    }
}
