using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.Tooling;

namespace NukeBuildHelpers;

public partial interface INukeBuildHelpers : INukeBuild
{
    [Parameter("Args for target")]
    string Args => TryGetValue(() => Args);

    [GitRepository]
    GitRepository Repository => TryGetValue(() => Repository);

    [PathVariable]
    Tool Git => TryGetValue(() => Git);

    [PathVariable]
    Tool Gh => TryGetValue(() => Gh);
}
