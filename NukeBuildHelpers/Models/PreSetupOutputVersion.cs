namespace NukeBuildHelpers;

internal class PreSetupOutputVersion
{
    public required string AppId { get; init; }

    public required string AppName { get; init; }

    public required string Environment { get; init; }

    public required string Version { get; init; }

    public required bool HasRelease { get; init; }
}
