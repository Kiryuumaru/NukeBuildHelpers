using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using NukeBuildHelpers.Models;
using Octokit;
using Semver;
using Serilog;
using System.Text.Json;

namespace NukeBuildHelpers;

partial class BaseNukeBuildHelpers
{
    Target INukeBuildHelpers.Bump => _ => _.Executes(() => BumpRelease(TargetParams["bump"]));

    Target INukeBuildHelpers.BumpAlpha => _ => _.Executes(() => BumpRelease("alpha"));

    Target INukeBuildHelpers.BumpBeta => _ => _.Executes(() => BumpRelease("beta"));

    Target INukeBuildHelpers.BumpRc => _ => _.Executes(() => BumpRelease("rc"));

    Target INukeBuildHelpers.BumpRtm => _ => _.Executes(() => BumpRelease("rtm"));

    Target INukeBuildHelpers.BumpPrerelease => _ => _.Executes(() => BumpRelease("prerelease"));

    Target INukeBuildHelpers.BumpPatch => _ => _.Executes(() => BumpRelease("patch"));

    Target INukeBuildHelpers.BumpMinor => _ => _.Executes(() => BumpRelease("minor"));

    Target INukeBuildHelpers.BumpMajor => _ => _.Executes(() => BumpRelease("major"));
}
