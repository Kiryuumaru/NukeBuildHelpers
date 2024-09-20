using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.RunContext.Interfaces;

namespace NukeBuildHelpers.RunContext.Models;

internal abstract class RunContext : IRunContext
{
    public required RunType RunType { get; init; }

    RunType IRunContext.RunType { get => RunType; }
}
