namespace NukeBuildHelpers.Models;

/// <summary>
/// Represents the context for running an application pipeline.
/// </summary>
public class AppPipelineRunContext : AppRunContext
{
    /// <summary>
    /// Gets the application version for the pipeline run.
    /// </summary>
    public AppVersion AppVersion { get; internal set; } = null!;
}
