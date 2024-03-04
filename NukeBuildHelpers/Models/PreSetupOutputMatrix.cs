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

public class PreSetupOutputMatrix
{
    public required string AppId { get; init; }

    public required string AppName { get; init; }

    public required bool HasRelease { get; init; }

    public required string IdsToRun { get; init; }

    public required string RunsOn { get; init; }

    public required string BuildScript { get; init; }
}
