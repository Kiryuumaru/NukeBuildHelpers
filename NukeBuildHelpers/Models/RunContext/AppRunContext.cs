using NukeBuildHelpers.Enums;

namespace NukeBuildHelpers.Models;

public abstract class AppRunContext : RunContext
{
    public required RunType RunType { get; init; }
}
