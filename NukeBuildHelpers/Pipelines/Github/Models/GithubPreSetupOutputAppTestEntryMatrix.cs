using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NukeBuildHelpers.Pipelines.Common.Models;

namespace NukeBuildHelpers.Pipelines.Github.Models;

internal class GithubPreSetupOutputAppTestEntryMatrix : PreSetupOutputAppTestEntryMatrix
{
    public required string NukeRunsOn { get; init; }

    public override string NukeRunnerName { get; } = "github";
}
