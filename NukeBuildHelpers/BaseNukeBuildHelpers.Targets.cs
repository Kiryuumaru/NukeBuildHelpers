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
    Target INukeBuildHelpers.Bump => _ => _
        .Executes(() =>
        {
            var bumpVal = TargetParams["bump"];

            Dictionary<string, List<SemVersion>> allVersions = new();
            foreach (var tag in Git.Invoke("tag -l", logOutput: false, logInvocation: false))
            {
                if (!SemVersion.TryParse(tag.Text, SemVersionStyles.Strict, out SemVersion tagSemver))
                {
                    continue;
                }
                string env = tagSemver.IsPrerelease ? tagSemver.PrereleaseIdentifiers[0].Value.ToLowerInvariant() : "";
                if (allVersions.TryGetValue(env, out List<SemVersion> versions))
                {
                    versions.Add(tagSemver);
                }
                else
                {
                    versions = new()
                    {
                        tagSemver
                    };
                    allVersions.Add(env, versions);
                }
            }

            SemVersion versionToBump = null;
            foreach (var allVersion in allVersions.ToList().OrderByDescending(i => i.Key))
            {
                allVersion.Value.Sort(SemVersion.PrecedenceComparer);
                if (string.IsNullOrEmpty(allVersion.Key))
                {
                    Log.Information("Last version for main releases is {currentVersion}", allVersion.Value.Last());
                }
                else
                {
                    Log.Information("Last version for {env} is {currentVersion}", allVersion.Key, allVersion.Value.Last());
                }
            }

            if (versionToBump.IsPrerelease)
            {
                Log.Information("Version to bump is {versionToBump} to {env}", versionToBump, versionToBump.PrereleaseIdentifiers[0].Value.ToLowerInvariant());
            }
            else
            {
                Log.Information("Version to bump is {versionToBump} to main releases", versionToBump);
            }

            SemVersion currentVersion = new(0);
            foreach (var tag in Git.Invoke("tag -l", logOutput: false, logInvocation: false))
            {
                if (!SemVersion.TryParse(tag.Text, SemVersionStyles.Strict, out SemVersion tagSemver))
                {
                    continue;
                }
                if (string.IsNullOrEmpty(bumpVal))
                {
                    if (tagSemver.IsPrerelease)
                    {
                        continue;
                    }
                }
                else
                {
                    if (!tagSemver.IsPrerelease)
                    {
                        continue;
                    }
                    if (bumpVal.ToLowerInvariant() != tagSemver.PrereleaseIdentifiers[0].Value.ToLowerInvariant())
                    {
                        continue;
                    }
                }
                if (SemVersion.ComparePrecedence(currentVersion, tagSemver) > 0)
                {
                    continue;
                }
                currentVersion = tagSemver;
            }

            int sss = 1;
        });
}
