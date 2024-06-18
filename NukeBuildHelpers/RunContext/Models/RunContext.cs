using NukeBuildHelpers.Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.RunContext.Interfaces;

internal abstract class RunContext : IRunContext
{
    public required RunType RunType { get; init; }

    RunType IRunContext.RunType { get => RunType; }
}
