using NukeBuildHelpers.Enums;

namespace NukeBuildHelpers.Models;

/// <summary>
/// Represents the context for running an application test.
/// </summary>
public class AppTestRunContext : RunContext
{
    /// <summary>
    /// Gets or sets the type of test run for the application.
    /// </summary>
    public RunTestType RunTestType { get; internal set; }
}
