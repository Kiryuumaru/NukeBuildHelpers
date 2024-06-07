using Semver;

namespace NukeBuildHelpers;

/// <summary>
/// Represents an application version.
/// </summary>
public class AppVersion
{
    /// <summary>
    /// Gets or sets the application ID.
    /// </summary>
    public required string AppId { get; init; }

    /// <summary>
    /// Gets or sets the environment for the version.
    /// </summary>
    public required string Environment { get; init; }

    /// <summary>
    /// Gets or sets the semantic version.
    /// </summary>
    public required SemVersion Version { get; init; }

    /// <summary>
    /// Gets or sets the build ID.
    /// </summary>
    public required long BuildId { get; init; }

    /// <summary>
    /// Gets or sets the release notes for the version.
    /// </summary>
    public required string ReleaseNotes { get; init; }
}
