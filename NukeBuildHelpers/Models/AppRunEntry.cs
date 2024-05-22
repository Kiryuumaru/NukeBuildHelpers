using Semver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Models;

internal class AppRunEntry
{
    public required AppEntry AppEntry { get; init; }

    public required string Env { get; init; }

    public required SemVersion Version { get; init; }

    public required bool HasRelease { get; init; }
}
