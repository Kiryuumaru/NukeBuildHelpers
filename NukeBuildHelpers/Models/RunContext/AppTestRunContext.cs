using NukeBuildHelpers.Enums;

namespace NukeBuildHelpers.Models;

public class AppTestRunContext : RunContext
{
    public required RunTestType RunTestType { get; init; }
}
