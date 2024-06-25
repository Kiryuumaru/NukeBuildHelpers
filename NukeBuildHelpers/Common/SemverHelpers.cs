using Semver;

namespace NukeBuildHelpers.Common;

internal static class SemverHelpers
{
    internal static bool IsVersionEmpty(SemVersion? semVersion)
    {
        return semVersion == null || semVersion.Major == 0 && semVersion.Minor == 0 && semVersion.Patch == 0;
    }
}
