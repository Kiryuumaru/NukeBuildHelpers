namespace NukeBuildHelpers.Pipelines.Common.Models;

internal abstract class PreSetupOutputMatrix
{
    public required string EntryId { get; init; }

    public required string EntryName { get; init; }

    public required string EntryIdsToRun { get; init; }

    public required string Environment { get; init; }

    public required string RunScript { get; init; }

    public required string CacheInvalidator { get; init; }

    public required string RunClassification { get; init; }

    public required string RunIdentifier { get; init; }

    public abstract string NukeRunnerName { get; }
}
