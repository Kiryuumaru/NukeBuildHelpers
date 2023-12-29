using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using NukeBuildHelpers.Models;
using Octokit;
using Semver;
using Serilog;
using Serilog.Events;
using System.Text.Json;

namespace NukeBuildHelpers;

partial class BaseNukeBuildHelpers
{
    public Target GenerateAppEntry => _ => _
        .Description("Generates app entry template, with --args \"path={path}\"")
        .Executes(() => {
            GetOrFail(() => SplitArgs, out var splitArgs);

            splitArgs.TryGetValue("path", out string pathRaw);

            AbsolutePath absolutePath = RootDirectory / "appentry.sample.json";
            if (!string.IsNullOrEmpty(pathRaw))
            {
                absolutePath = AbsolutePath.Create(absolutePath);
            }

            Log.Information("Generating app config to \"{path}\"", absolutePath);

            AppEntryConfig config = new()
            {
                MainRelease = true,
                BuildsOn = Enums.BuildsOnType.Ubuntu2204
            };

            File.WriteAllText(absolutePath, JsonSerializer.Serialize(config, jsonSerializerOptions));

            Log.Information("Generate done");
        });

    public Target GenerateAppTestEntry => _ => _
        .Description("Generates app test entry template, with --args \"path={path}\"")
        .Executes(() => {
            GetOrFail(() => SplitArgs, out var splitArgs);

            splitArgs.TryGetValue("path", out string pathRaw);

            AbsolutePath absolutePath = RootDirectory / "apptestentry.sample.json";
            if (!string.IsNullOrEmpty(pathRaw))
            {
                absolutePath = AbsolutePath.Create(absolutePath);
            }

            Log.Information("Generating app config to \"{path}\"", absolutePath);

            AppTestEntryConfig config = new()
            {
                BuildsOn = Enums.BuildsOnType.Ubuntu2204
            };

            File.WriteAllText(absolutePath, JsonSerializer.Serialize(config, jsonSerializerOptions));

            Log.Information("Generate done");
        });

    public Target Version => _ => _
        .Description("Shows the current version from all releases, with --args \"appid={appid}\"")
        .Executes(() =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntryConfigs);

            splitArgs.TryGetValue("appid", out string appId);

            GetOrFail(appId, appEntryConfigs, out appId, out var appEntry);
            GetOrFail(() => GetAllVersions(appId, appEntryConfigs), out var allVersions);

            Log.Information("Commit: {Value}", Repository.Commit);
            Log.Information("Branch: {Value}", Repository.Branch);
            foreach (var groupKey in allVersions.GroupKeySorted)
            {
                string env;
                if (string.IsNullOrEmpty(groupKey))
                {
                    env = "main";
                }
                else
                {
                    env = groupKey;
                }
                Log.Information("Current {env}: {currentVersion}", env, allVersions.VersionGrouped[groupKey].Last());
            }
        });

    public Target Bump => _ => _
        .Description("Bumps the version by tagging and validating tags, with --args \"version={semver};appid={appid};strict_branch={boolean}\"")
        .DependsOn(Version)
        .Executes(() =>
        {
            static void logArgs(string msg, params object[] args)
            {
                Log.Information(messageTemplate:
                    msg +
                    "\"" +
                    "version={version};" +
                    "appid={appid};" +
                    "strict_branch={strict_branch}" +
                    "\"",
                    propertyValues: args
                );
            }

            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntryConfigs);

            // ---------- Args fetch ----------

            splitArgs.TryGetValue("version", out string versionRaw);
            splitArgs.TryGetValue("appid", out string appId);
            splitArgs.TryGetValue("strict_branch", out string strictBranchRaw);

            // Use plain Args as versionRaw if version args is not provided
            versionRaw = string.IsNullOrEmpty(versionRaw) ? Args : versionRaw;

            logArgs("Bump version with --args ", versionRaw, appId, strictBranchRaw);

            // ---------- Args validation ----------

            GetOrFail(appId, appEntryConfigs, out appId, out var appEntryConfig);
            GetOrFail(() => GetAllVersions(appId, appEntryConfigs), out var allVersions);
            GetOrFail(versionRaw, out SemVersion version);
            GetOrFail(strictBranchRaw, out bool strictBranch);

            logArgs("Validated --args ", version, appId, strictBranch);

            // Fail if strictBranch is true and current branch is not on the proper bump branch
            string envIdentifier;
            string env;
            if (version.IsPrerelease)
            {
                if (strictBranch && Repository.Branch.ToLowerInvariant() != version.PrereleaseIdentifiers[0])
                {
                    Assert.Fail($"{version} should bump on {version.PrereleaseIdentifiers[0]} branch");
                    return;
                }
                envIdentifier = version.PrereleaseIdentifiers[0];
                env = version.PrereleaseIdentifiers[0];
            }
            else
            {
                if (strictBranch &&
                    Repository.Branch.ToLowerInvariant() != "master" &&
                    Repository.Branch.ToLowerInvariant() != "main" &&
                    Repository.Branch.ToLowerInvariant() != "prod")
                {
                    Assert.Fail($"{version} should bump on main branch");
                    return;
                }
                envIdentifier = "";
                env = "main";
            }

            if (allVersions.VersionGrouped.ContainsKey(envIdentifier))
            {
                var lastVersion = allVersions.VersionGrouped[envIdentifier].Last();
                // Fail if the version is already released
                if (SemVersion.ComparePrecedence(lastVersion, version) == 0)
                {
                    Assert.Fail($"The latest version in the {env} releases is already {version}");
                    return;
                }
                // Fail if the version is behind the latest release
                if (SemVersion.ComparePrecedence(lastVersion, version) > 0)
                {
                    Assert.Fail($"{version} is behind the latest version {lastVersion} in the {env} releases");
                    return;
                }
            }

            // ---------- Apply bump ----------

            logArgs("Apply bump with validated --args ", version, appId, strictBranch);

            Log.Information("Pushing bump version {ver}...", versionRaw);

            Git.Invoke($"tag {versionRaw}", logInvocation: false, logOutput: false);
            Git.Invoke($"push origin {versionRaw}", logInvocation: false, logOutput: false);

            Log.Information("Bump done");
        });
}
