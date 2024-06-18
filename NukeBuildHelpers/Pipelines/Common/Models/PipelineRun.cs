using NukeBuildHelpers.Pipelines.Common.Enums;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Pipelines.Common.Models;

internal class PipelineRun
{
    public required IPipeline Pipeline { get; init; }

    public required PipelineInfo PipelineInfo { get; init; }

    public required PipelineType PipelineType { get; init; }
}
