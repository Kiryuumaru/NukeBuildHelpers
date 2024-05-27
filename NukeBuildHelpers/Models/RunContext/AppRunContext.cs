using NukeBuildHelpers.Enums;

namespace NukeBuildHelpers.Models.RunContext;

public abstract class AppRunContext : RunContext
{
    public required RunType RunType { get; init; }
}
