using Microsoft.Build.Tasks;
using Nuke.Common;
using Nuke.Common.IO;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Enums;
using Semver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace NukeBuildHelpers;

internal class PreSetupOutputMatrix
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string IdsToRun { get; init; }

    public required string RunsOn { get; init; }

    public required string BuildScript { get; init; }
}
