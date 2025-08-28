using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Entry.Enums;
using NukeBuildHelpers.Runner.Models;

namespace NukeBuildHelpers.Entry.Models;

internal class EntrySetup
{
    public required string Id { get; init; }

    public required Dictionary<string, RunType> RunTypes { get; init; }

    public required bool Condition { get; init; }

    public required RunnerOSSetup RunnerOSSetup { get; init; }

    public required string CacheInvalidator { get; init; }

    public required string[] CachePaths { get; init; }

    public required int CheckoutFetchDepth { get; init; }

    public required bool CheckoutFetchTags { get; init; }

    public required SubmoduleCheckoutType CheckoutSubmodules { get; init; }
}
