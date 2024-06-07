using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Pipelines.Models;

internal class GithubPreSetupOutputAppTestEntryMatrix : PreSetupOutputAppTestEntryMatrix
{
    public required string RunsOn { get; init; }

    public override string RunnerName { get; } = "github";
}
