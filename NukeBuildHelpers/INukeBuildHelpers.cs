using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities;
using Octokit;
using Semver;
using Serilog;
using System.Text.Json;

namespace NukeBuildHelpers;

public partial interface INukeBuildHelpers : INukeBuild
{
    [Parameter("Args for target")]
    string Args => TryGetValue(() => Args);

    [GitRepository]
    GitRepository Repository => TryGetValue(() => Repository);

    [PathVariable]
    Tool Git => TryGetValue(() => Git);

    Target Version { get; }

    Target Bump { get; }
}
