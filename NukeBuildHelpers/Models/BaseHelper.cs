using Nuke.Common.IO;
using NukeBuildHelpers.Pipelines.Enums;

namespace NukeBuildHelpers;

/// <summary>
/// Represents the base helper class providing common properties and methods for Nuke builds.
/// </summary>
public abstract class BaseHelper
{
    /// <summary>
    /// Gets the root directory of the Nuke build.
    /// </summary>
    public static AbsolutePath RootDirectory => Nuke.Common.NukeBuild.RootDirectory;

    /// <summary>
    /// Gets the temporary directory used by the Nuke build.
    /// </summary>
    public static AbsolutePath TemporaryDirectory => Nuke.Common.NukeBuild.TemporaryDirectory;

    /// <summary>
    /// Gets the output directory used by the Nuke build.
    /// </summary>
    public static AbsolutePath OutputDirectory => BaseNukeBuildHelpers.OutputDirectory;

    /// <summary>
    /// Gets the cache directory used by the Nuke build.
    /// </summary>
    public static AbsolutePath CacheDirectory => BaseNukeBuildHelpers.CacheDirectory;

    /// <summary>
    /// Gets or sets the Nuke build helpers instance.
    /// </summary>
    public BaseNukeBuildHelpers NukeBuild { get; internal set; } = null!;

    /// <summary>
    /// Gets or sets the type of pipeline.
    /// </summary>
    public PipelineType PipelineType { get; internal set; }
}
