using NukeBuildHelpers.Enums;

namespace NukeBuildHelpers.Models.RunContext;

public class AppTestRunContext : RunContext
{
    public required RunTestType RunTestType { get; init; }
}
