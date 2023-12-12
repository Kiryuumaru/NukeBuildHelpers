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
    [Parameter("Parameters for target")]
    public string TargetParams => TryGetValue(() => TargetParams);

    [GitRepository]
    GitRepository Repository => TryGetValue(() => Repository);

    [PathVariable]
    Tool Git => TryGetValue(() => Git);

    protected Target Bump { get; }

    protected Target BumpAlpha { get; }

    protected Target BumpBeta { get; }

    protected Target BumpRc { get; }

    protected Target BumpRtm { get; }

    protected Target BumpPrerelease { get; }

    protected Target BumpPatch { get; }

    protected Target BumpMinor { get; }

    protected Target BumpMajor { get; }
}
