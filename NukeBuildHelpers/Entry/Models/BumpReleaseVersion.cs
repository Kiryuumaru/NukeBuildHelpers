namespace NukeBuildHelpers.Entry.Models;

/// <summary>
/// Represents a bumped release version of an application with additional release notes.
/// </summary>
public class BumpReleaseVersion : AppVersion
{
    /// <summary>
    /// Gets the release notes associated with this version bump.
    /// </summary>
    public required string ReleaseNotes { get; init; }
}
