namespace NukeBuildHelpers.Entry.Models;

/// <summary>
/// Represents a release version tied to a specific pull request.
/// </summary>
public class PullRequestReleaseVersion : AppVersion
{
    /// <summary>
    /// Gets the pull request number associated with this release version.
    /// </summary>
    public required long PullRequestNumber { get; init; }
}
