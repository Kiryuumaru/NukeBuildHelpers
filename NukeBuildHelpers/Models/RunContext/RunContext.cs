using Nuke.Common.IO;

namespace NukeBuildHelpers.Models;

/// <summary>
/// Represents the base context for running operations.
/// </summary>
public class RunContext
{
    /// <summary>
    /// Gets the output directory for the run.
    /// </summary>
    public AbsolutePath OutputDirectory { get; internal set; } = null!;
}
