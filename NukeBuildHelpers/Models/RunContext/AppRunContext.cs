using NukeBuildHelpers.Enums;

namespace NukeBuildHelpers.Models;

/// <summary>
/// Represents the context for running an application.
/// </summary>
public abstract class AppRunContext : RunContext
{
    /// <summary>
    /// Gets the type of run for the application.
    /// </summary>
    public RunType RunType { get; internal set; }
}
