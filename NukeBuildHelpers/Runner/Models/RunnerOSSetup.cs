namespace NukeBuildHelpers.Runner.Models;

internal class RunnerOSSetup
{
    public required string Name { get; init; }

    public required string RunnerPipelineOS { get; init; }

    public required string RunScript { get; init; }
}
