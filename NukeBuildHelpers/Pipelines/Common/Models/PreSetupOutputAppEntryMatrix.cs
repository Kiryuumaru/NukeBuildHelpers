namespace NukeBuildHelpers.Pipelines.Common.Models;

internal abstract class PreSetupOutputAppEntryMatrix : PreSetupOutputMatrix
{
    public required string NukeVersion { get; init; }
}
