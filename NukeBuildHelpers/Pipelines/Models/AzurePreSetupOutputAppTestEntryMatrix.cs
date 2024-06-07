using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Pipelines.Models;

internal class AzurePreSetupOutputAppTestEntryMatrix : PreSetupOutputAppTestEntryMatrix
{
    public required string? NukePoolName { get; init; }

    public required string? NukePoolVMImage { get; init; }

    public override string NukeRunnerName { get; } = "azure";
}
