namespace NukeBuildHelpers.Models;

/// <summary>
/// Represents the context for running an application pull request pipeline.
/// </summary>
public class AppPullRequestRunContext : AppPipelineRunContext
{
    /// <summary>
    /// Gets the pull request number.
    /// </summary>
    public long PullRequestNumber { get; internal set; }
}
