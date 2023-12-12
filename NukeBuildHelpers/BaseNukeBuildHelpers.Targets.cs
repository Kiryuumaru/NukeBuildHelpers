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
    Target INukeBuildHelpers.Version => _ => _
        .Executes(() =>
        {
            var currentVersions = GetCurrentVersions();
            foreach (var groupKey in currentVersions.GroupKeySorted)
            {
                if (string.IsNullOrEmpty(groupKey))
                {
                    Log.Information("Current main releases is {currentVersion}", currentVersions.VersionGrouped[groupKey].Last());
                }
                else
                {
                    Log.Information("Current {env} is {currentVersion}", groupKey, currentVersions.VersionGrouped[groupKey].Last());
                }
            }
        });

    Target INukeBuildHelpers.Bump => _ => _.
        Executes(() =>
        {
            Dictionary<string, int> bumps = new();
            foreach (var arg in TargetParams)
            {
                if (int.TryParse(arg.Value, out int bump))
                {
                    bumps.Add(arg.Key.ToLowerInvariant(), bump);
                }
            }

            BumpRelease(bumps);
        });

    Target INukeBuildHelpers.BumpAlpha => _ => _.Executes(() => BumpRelease(new Dictionary<string, int>() { { "alpha", 1 } }));

    Target INukeBuildHelpers.BumpBeta => _ => _.Executes(() => BumpRelease(new Dictionary<string, int>() { { "beta", 1 } }));

    Target INukeBuildHelpers.BumpRc => _ => _.Executes(() => BumpRelease(new Dictionary<string, int>() { { "rc", 1 } }));

    Target INukeBuildHelpers.BumpRtm => _ => _.Executes(() => BumpRelease(new Dictionary<string, int>() { { "rtm", 1 } }));

    Target INukeBuildHelpers.BumpPrerelease => _ => _.Executes(() => BumpRelease(new Dictionary<string, int>() { { "prerelease", 1 } }));

    Target INukeBuildHelpers.BumpPatch => _ => _.Executes(() => BumpRelease(new Dictionary<string, int>() { { "patch", 1 } }));

    Target INukeBuildHelpers.BumpMinor => _ => _.Executes(() => BumpRelease(new Dictionary<string, int>() { { "minor", 1 } }));

    Target INukeBuildHelpers.BumpMajor => _ => _.Executes(() => BumpRelease(new Dictionary<string, int>() { { "major", 1 } }));
}
