using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Pipelines.Models;

internal class GithubPreSetupOutputAppTestEntryMatrix : PreSetupOutputAppTestEntryMatrix
{
    public required string NukeRunsOn { get; init; }

    public override string NukeRunnerName { get; } = "github";
}
