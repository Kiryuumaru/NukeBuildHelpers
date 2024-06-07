using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Pipelines.Models;

internal class AzurePreSetupOutputAppTestEntryMatrix : PreSetupOutputAppTestEntryMatrix
{
    public required string? PoolName { get; init; }

    public required string? PoolVMImage { get; init; }

    public override string RunnerName { get; } = "azure";
}
