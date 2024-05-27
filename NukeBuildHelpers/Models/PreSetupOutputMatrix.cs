namespace NukeBuildHelpers;

internal class PreSetupOutputMatrix
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string IdsToRun { get; init; }

    public required string Environment { get; init; }

    public required string RunsOn { get; init; }

    public required string BuildScript { get; init; }
}
