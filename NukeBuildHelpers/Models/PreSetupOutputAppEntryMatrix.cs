namespace NukeBuildHelpers;

internal abstract class PreSetupOutputAppEntryMatrix : PreSetupOutputMatrix
{
    public required string NukeVersion { get; init; }
}
