using NukeBuildHelpers.Pipelines.Common.Enums;
using NukeBuildHelpers.Runner.Models;

namespace NukeBuildHelpers.Runner.Abstraction;

/// <summary>
/// Represents a base class for runner operating systems.
/// </summary>
public abstract class RunnerOS
{
    /// <summary>
    /// Gets the latest Ubuntu runner OS.
    /// </summary>
    public static RunnerOS UbuntuLatest { get; } = new RunnerOSUbuntuLatest();

    /// <summary>
    /// Gets the Ubuntu 22.04 runner OS.
    /// </summary>
    public static RunnerOS Ubuntu2204 { get; } = new RunnerOSUbuntu2204();

    /// <summary>
    /// Gets the latest Windows runner OS.
    /// </summary>
    public static RunnerOS WindowsLatest { get; } = new RunnerOSWindowsLatest();

    /// <summary>
    /// Gets the Windows 2022 runner OS.
    /// </summary>
    public static RunnerOS Windows2022 { get; } = new RunnerOSWindows2022();

    /// <summary>
    /// Gets the name of the runner.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the pipeline-specific operating system for this runner OS.
    /// </summary>
    /// <param name="pipelineType">The type of pipeline.</param>
    /// <returns>The pipeline-specific operating system.</returns>
    public abstract object GetPipelineOS(PipelineType pipelineType);

    /// <summary>
    /// Gets the run script for the specified pipeline type.
    /// </summary>
    /// <param name="pipelineType">The type of pipeline.</param>
    /// <returns>The run script for the specified pipeline type.</returns>
    public abstract string GetRunScript(PipelineType pipelineType);
}
