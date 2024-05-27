namespace NukeBuildHelpers.Models.RunContext;

public class AppPipelineRunContext : AppRunContext
{
    public required AppVersion AppVersion { get; init; }
}
