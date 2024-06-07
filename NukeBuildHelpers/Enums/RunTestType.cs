namespace NukeBuildHelpers.Enums;

/// <summary>
/// Represents the type of test run.
/// </summary>
[Flags]
public enum RunTestType
{
    /// <summary>
    /// No test run.
    /// </summary>
    None = 0b00,

    /// <summary>
    /// Local test run only, excluding pipeline execution.
    /// </summary>
    Local = 0b01,

    /// <summary>
    /// Test run for the target app entry only if it's also running.
    /// </summary>
    Target = 0b10,

    /// <summary>
    /// Test run always, regardless of other conditions.
    /// </summary>
    All = 0b11,
}
