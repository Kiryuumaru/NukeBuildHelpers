using NukeBuildHelpers.Common.Enums;

namespace NukeBuildHelpers.RunContext.Interfaces;

internal abstract class RunContext : IRunContext
{
    public required RunType RunType { get; init; }

    RunType IRunContext.RunType { get => RunType; }
}
