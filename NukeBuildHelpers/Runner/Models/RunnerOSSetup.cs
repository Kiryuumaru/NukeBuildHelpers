using NukeBuildHelpers.Runner.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Runner.Models;

internal class RunnerOSSetup
{
    public required string Name { get; init; }

    public required string RunnerPipelineOS { get; init; }

    public required string RunScript { get; init; }
}
