using Microsoft.Build.Tasks;
using Nuke.Common;
using Nuke.Common.IO;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace NukeBuildHelpers;

public abstract class BaseHelper
{
    public static AbsolutePath RootDirectory => Nuke.Common.NukeBuild.RootDirectory;

    public static AbsolutePath TemporaryDirectory => Nuke.Common.NukeBuild.TemporaryDirectory;

    public static AbsolutePath OutputDirectory => BaseNukeBuildHelpers.OutputDirectory;

    public BaseNukeBuildHelpers NukeBuild { get; internal set; } = null!;

    public PipelineType PipelineType { get; internal set; }
}
