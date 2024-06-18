using Nuke.Common.IO;
using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.Runner.Abstraction;
using NukeBuildHelpers.Runner.Models;
using Semver;

namespace NukeBuildHelpers.Common.Models;

internal class EntrySetup
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required bool Condition { get; init; }

    public required RunnerOSSetup RunnerOSSetup { get; init; }

    public required string CacheInvalidator { get; init; }

    public required string[] CachePaths { get; init; }

    public required RunType RunType { get; init; }
}
