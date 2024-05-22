using NukeBuildHelpers.Enums;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Models;

public class PipelineInfo
{
    public required string Branch { get; init; }

    public required TriggerType TriggerType { get; init; }

    public required long PrNumber { get; init; }
}
