namespace NukeBuildHelpers.Models;

public class AppPullRequestRunContext : AppPipelineRunContext
{
    public long PullRequestNumber { get; internal set; }
}
