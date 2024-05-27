namespace NukeBuildHelpers.Models.RunContext;

public class AppPullRequestRunContext : AppPipelineRunContext
{
    public long PullRequestNumber { get; internal set; }
}
