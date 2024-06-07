namespace NukeBuildHelpers.Enums;

/// <summary>
/// Represents the type of run.
/// </summary>
[Flags]
public enum RunType
{
    /// <summary>
    /// No specific run type.
    /// </summary>
    None = 0b0000,

    /// <summary>
    /// Local run only, excluding pipeline execution.
    /// </summary>
    Local = 0b0001,

    /// <summary>
    /// Run triggered by a pull request.
    /// </summary>
    PullRequest = 0b0010,

    /// <summary>
    /// Run triggered by a commit on environment branches, excluding pull request.
    /// </summary>
    Commit = 0b0100,

    /// <summary>
    /// Run triggered by a version bump.
    /// </summary>
    Bump = 0b1000,

    /// <summary>
    /// Run always, regardless of other conditions.
    /// </summary>
    All = 0b1111
}
