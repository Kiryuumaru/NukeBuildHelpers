using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.Tooling;

namespace NukeBuildHelpers;

/// <summary>
/// Provides an interface for Nuke build helpers.
/// </summary>
public partial interface INukeBuildHelpers : INukeBuild
{
    /// <summary>
    /// Gets the arguments passed to the build.
    /// </summary>
    [Parameter("Args for target")]
    string Args => TryGetValue(() => Args);

    /// <summary>
    /// Gets the Git repository associated with the build.
    /// </summary>
    [GitRepository]
    GitRepository Repository => TryGetValue(() => Repository);

    /// <summary>
    /// Gets the Git tool used in the build.
    /// </summary>
    [PathVariable]
    Tool Git => TryGetValue(() => Git);

    /// <summary>
    /// Gets the GitHub tool used in the build.
    /// </summary>
    [PathVariable]
    Tool Gh => TryGetValue(() => Gh);
}
