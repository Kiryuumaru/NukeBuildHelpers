namespace NukeBuildHelpers.Models;

public class AppPipelineRunContext : AppRunContext
{
    public required AppVersion AppVersion { get; init; }
}
