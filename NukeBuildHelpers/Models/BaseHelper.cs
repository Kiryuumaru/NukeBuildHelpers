using Nuke.Common.IO;
using NukeBuildHelpers.Enums;

namespace NukeBuildHelpers;

public abstract class BaseHelper
{
    public static AbsolutePath RootDirectory => Nuke.Common.NukeBuild.RootDirectory;

    public static AbsolutePath TemporaryDirectory => Nuke.Common.NukeBuild.TemporaryDirectory;

    public static AbsolutePath OutputDirectory => BaseNukeBuildHelpers.OutputDirectory;

    public BaseNukeBuildHelpers NukeBuild { get; internal set; } = null!;

    public PipelineType PipelineType { get; internal set; }
}
