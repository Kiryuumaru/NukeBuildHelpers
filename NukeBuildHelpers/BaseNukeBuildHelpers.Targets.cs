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
    public Target Version => _ => _
        .Description("Shows the current version from all releases")
        .Executes(() =>
        {
            AllVersions = GetCurrentVersions();
            Log.Information("Commit: {Value}", Repository.Commit);
            Log.Information("Branch: {Value}", Repository.Branch);
            Log.Information("Tags: {Value}", Repository.Tags);
            foreach (var groupKey in AllVersions.GroupKeySorted)
            {
                if (string.IsNullOrEmpty(groupKey))
                {
                    Log.Information("Current main: {currentVersion}", AllVersions.VersionGrouped[groupKey].Last());
                }
                else
                {
                    Log.Information("Current {env}: {currentVersion}", groupKey, AllVersions.VersionGrouped[groupKey].Last());
                }
            }
        });

    public Target Bump => _ => _
        .Description("Bumps the version by tagging and validating tags")
        .DependsOn(Version)
        .OnlyWhenDynamic(() => AllVersions != null)
        .Executes(() =>
        {
            if (!SplitArgs.TryGetValue("version", out string versionRaw))
            {
                versionRaw = Args;
            }

            Log.Information("Validating bump version {ver}...", versionRaw);

            if (!SemVersion.TryParse(versionRaw, SemVersionStyles.Strict, out SemVersion version))
            {
                Assert.Fail($"{Args} is not a valid semver version");
                return;
            }

            if (version.IsPrerelease)
            {
                if (AllVersions.VersionGrouped.ContainsKey(version.PrereleaseIdentifiers[0]))
                {
                    var lastVersion = AllVersions.VersionGrouped[version.PrereleaseIdentifiers[0]].Last();
                    if (SemVersion.ComparePrecedence(lastVersion, version) == 0)
                    {
                        Assert.Fail($"The latest version in the {lastVersion.PrereleaseIdentifiers[0]} releases is already {Args}");
                        return;
                    }
                    if (SemVersion.ComparePrecedence(lastVersion, version) > 0)
                    {
                        Assert.Fail($"{Args} is behind the latest version {lastVersion} in the {lastVersion.PrereleaseIdentifiers[0]} releases");
                        return;
                    }
                }
            }
            else
            {
                if (AllVersions.VersionGrouped.ContainsKey(""))
                {
                    var lastVersion = AllVersions.VersionGrouped[""].Last();
                    if (SemVersion.ComparePrecedence(lastVersion, version) == 0)
                    {
                        Assert.Fail($"The latest version in the main releases is already {Args}");
                        return;
                    }
                    if (SemVersion.ComparePrecedence(lastVersion, version) > 0)
                    {
                        Assert.Fail($"{Args} is behind the latest version {lastVersion} in the main releases");
                        return;
                    }
                }
            }

            Log.Information("Pushing bump version {ver}...", versionRaw);

            Git.Invoke($"tag {versionRaw}", logInvocation: false, logOutput: false);
            Git.Invoke($"push origin {versionRaw}", logInvocation: false, logOutput: false);

            Log.Information("Bump done");
        });
}
