using Semver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Common;

internal static class SemverHelpers
{
    internal static bool IsVersionEmpty(SemVersion? semVersion)
    {
        return semVersion == null || semVersion.Major == 0 && semVersion.Minor == 0 && semVersion.Patch == 0;
    }
}
