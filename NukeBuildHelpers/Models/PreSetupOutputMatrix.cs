namespace NukeBuildHelpers;

internal abstract class PreSetupOutputMatrix
{
    public required string NukeEntryId { get; init; }

    public required string NukeEntryName { get; init; }

    public required string NukeEntryIdsToRun { get; init; }

    public required string NukeEnvironment { get; init; }

    public required string NukeRunScript { get; init; }

    public required string NukeCacheInvalidator { get; init; }

    public required string NukeRunClassification { get; init; }

    public required string NukeRunIdentifier { get; init; }

    public abstract string NukeRunnerName { get; }
}
