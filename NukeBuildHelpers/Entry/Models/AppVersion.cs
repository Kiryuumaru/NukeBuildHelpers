using Semver;

namespace NukeBuildHelpers.Entry.Models;

/// <summary>
/// Represents the version information of an application.
/// </summary>
public class AppVersion
{
    /// <summary>
    /// Gets the application ID.
    /// </summary>
    public required string AppId { get; init; }

    /// <summary>
    /// Gets the environment for which the version applies.
    /// </summary>
    public required string Environment { get; init; }

    /// <summary>
    /// Gets the semantic version of the application.
    /// </summary>
    public required SemVersion Version { get; init; }

    /// <summary>
    /// Gets the build ID associated with the version.
    /// </summary>
    public required long BuildId { get; init; }
}
