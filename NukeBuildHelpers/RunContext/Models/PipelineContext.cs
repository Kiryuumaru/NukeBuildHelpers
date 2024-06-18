using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Pipelines.Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.RunContext.Interfaces;

internal abstract class PipelineContext : RunContext, IPipelineContext
{
    public required PipelineType PipelineType { get; init; }

    PipelineType IPipelineContext.PipelineType { get => PipelineType; }
}
