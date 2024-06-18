using Semver;

namespace NukeBuildHelpers.Entry.Models;

public class BumpReleaseVersion : AppVersion
{
    public required string ReleaseNotes { get; init; }
}
