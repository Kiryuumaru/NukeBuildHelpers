using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Common.Models;
using Semver;

namespace NukeBuildHelpers.Common;

internal static class SemverHelpers
{
    internal static bool IsVersionEmpty(SemVersion? semVersion)
    {
        return semVersion == null || semVersion.Major == 0 && semVersion.Minor == 0 && semVersion.Patch == 0;
    }

    internal static SemVersion ApplyBumps(this SemVersion bumpVersion, IEnumerable<VersionBump> versionBumps)
    {
        foreach (var bumpPart in versionBumps)
        {
            switch (bumpPart.Part)
            {
                case VersionPart.Major:
                    bumpVersion = bumpVersion.WithMajor(bumpPart.IsIncrement ? bumpVersion.Major + bumpPart.BumpAssign : bumpPart.BumpAssign);
                    bumpVersion = bumpVersion.WithMinor(0);
                    bumpVersion = bumpVersion.WithPatch(0);
                    if (!string.IsNullOrEmpty(bumpVersion.Prerelease))
                    {
                        var prereleaseSplitFromMajor = bumpVersion.Prerelease.Split(".");
                        bumpVersion = bumpVersion.WithPrereleaseParsedFrom(prereleaseSplitFromMajor[0] + "." + 1);
                    }
                    break;
                case VersionPart.Minor:
                    bumpVersion = bumpVersion.WithMinor(bumpPart.IsIncrement ? bumpVersion.Minor + bumpPart.BumpAssign : bumpPart.BumpAssign);
                    bumpVersion = bumpVersion.WithPatch(0);
                    if (!string.IsNullOrEmpty(bumpVersion.Prerelease))
                    {
                        var prereleaseSplitFromMajor = bumpVersion.Prerelease.Split(".");
                        bumpVersion = bumpVersion.WithPrereleaseParsedFrom(prereleaseSplitFromMajor[0] + "." + 1);
                    }
                    break;
                case VersionPart.Patch:
                    bumpVersion = bumpVersion.WithPatch(bumpPart.IsIncrement ? bumpVersion.Patch + bumpPart.BumpAssign : bumpPart.BumpAssign);
                    if (!string.IsNullOrEmpty(bumpVersion.Prerelease))
                    {
                        var prereleaseSplitFromMajor = bumpVersion.Prerelease.Split(".");
                        bumpVersion = bumpVersion.WithPrereleaseParsedFrom(prereleaseSplitFromMajor[0] + "." + 1);
                    }
                    break;
                case VersionPart.Prerelease:
                    var prereleaseSplit = bumpVersion.Prerelease.Split(".");
                    bumpVersion = bumpVersion.WithPrereleaseParsedFrom(prereleaseSplit[0] + "." + (bumpPart.IsIncrement ? int.Parse(prereleaseSplit[1]) + bumpPart.BumpAssign : bumpPart.BumpAssign));
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
        return bumpVersion;
    }
}
