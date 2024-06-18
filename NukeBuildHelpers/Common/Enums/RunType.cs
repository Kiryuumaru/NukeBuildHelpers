namespace NukeBuildHelpers.Common.Enums;

/// <summary>
/// Represents the type of run.
/// </summary>
[Flags]
public enum RunType
{
    /// <summary>
    /// No specific run type.
    /// </summary>
    None = 0b00000,

    /// <summary>
    /// Local run only, excluding pipeline execution.
    /// </summary>
    Local = 0b00001,

    /// <summary>
    /// Run triggered by a pull request.
    /// </summary>
    PullRequest = 0b00010,

    /// <summary>
    /// Run triggered by a commit on environment branches, excluding pull request.
    /// </summary>
    Commit = 0b00100,

    /// <summary>
    /// Run triggered by a version bump.
    /// </summary>
    Bump = 0b01000,

    /// <summary>
    /// Run for the target app entry only if it's also running.
    /// </summary>
    Target = 0b10000,

    /// <summary>
    /// Run always, regardless of other conditions.
    /// </summary>
    All = 0b11111
}
