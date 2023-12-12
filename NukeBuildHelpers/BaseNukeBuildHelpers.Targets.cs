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
    private AllVersions allVersions = null;

    public Target Version => _ => _
        .Description("Shows the current version from all releases")
        .Executes(() =>
        {
            allVersions = GetCurrentVersions();
            Log.Information("Commit: {Value}", Repository.Commit);
            Log.Information("Branch: {Value}", Repository.Branch);
            Log.Information("Tags: {Value}", Repository.Tags);
            foreach (var groupKey in allVersions.GroupKeySorted)
            {
                if (string.IsNullOrEmpty(groupKey))
                {
                    Log.Information("Current main: {currentVersion}", allVersions.VersionGrouped[groupKey].Last());
                }
                else
                {
                    Log.Information("Current {env}: {currentVersion}", groupKey, allVersions.VersionGrouped[groupKey].Last());
                }
            }
        });

    public Target Bump => _ => _
        .Description("Bumps the version by tagging and validating tags")
        .DependsOn(Version)
        .OnlyWhenDynamic(() => allVersions != null)
        .Executes(() =>
        {
            if (!SemVersion.TryParse(TargetParams, SemVersionStyles.Strict, out SemVersion version))
            {
                Assert.Fail($"{TargetParams} is not a valid semver version");
                return;
            }

            if (version.IsPrerelease)
            {
                if (allVersions.VersionGrouped.ContainsKey(version.PrereleaseIdentifiers[0]))
                {
                    var lastVersion = allVersions.VersionGrouped[version.PrereleaseIdentifiers[0]].Last();
                    if (SemVersion.ComparePrecedence(lastVersion, version) == 0)
                    {
                        Assert.Fail($"The latest version in the {lastVersion.PrereleaseIdentifiers[0]} releases is already {TargetParams}");
                        return;
                    }
                    if (SemVersion.ComparePrecedence(lastVersion, version) > 0)
                    {
                        Assert.Fail($"{TargetParams} is behind the latest version {lastVersion} in the {lastVersion.PrereleaseIdentifiers[0]} releases");
                        return;
                    }
                }
            }
            else
            {
                if (allVersions.VersionGrouped.ContainsKey(""))
                {
                    var lastVersion = allVersions.VersionGrouped[""].Last();
                    if (SemVersion.ComparePrecedence(lastVersion, version) == 0)
                    {
                        Assert.Fail($"The latest version in the main releases is already {TargetParams}");
                        return;
                    }
                    if (SemVersion.ComparePrecedence(lastVersion, version) > 0)
                    {
                        Assert.Fail($"{TargetParams} is behind the latest version {lastVersion} in the main releases");
                        return;
                    }
                }
            }
        });
}
