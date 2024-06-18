using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Common.Models;
using NukeBuildHelpers.Entry.Models;

namespace NukeBuildHelpers.Pipelines.Common.Models;

internal class PipelinePreSetup
{
    public required string Branch { get; init; }

    public required TriggerType TriggerType { get; init; }

    public required string ReleaseNotes { get; init; }

    public required long BuildId { get; init; }

    public required long LastBuildId { get; init; }

    public required string Environment { get; init; }

    public required long PullRequestNumber { get; init; }

    public required List<string> TestEntries { get; init; }

    public required List<string> BuildEntries { get; init; }

    public required List<string> PublishEntries { get; init; }

    public required Dictionary<string, EntrySetup> EntrySetupMap { get; init; }

    public required Dictionary<string, AppRunEntry> AppRunEntryMap { get; init; }
}
