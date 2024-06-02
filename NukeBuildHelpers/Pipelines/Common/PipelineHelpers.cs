using NukeBuildHelpers.Pipelines.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Pipelines.Common;

internal static class PipelineHelpers
{
    internal static void BuildWorkflow<T>(BaseNukeBuildHelpers baseNukeBuildHelpers)
        where T : IPipeline
    {
        (Activator.CreateInstance(typeof(T), baseNukeBuildHelpers) as IPipeline)!.BuildWorkflow();
    }
}
