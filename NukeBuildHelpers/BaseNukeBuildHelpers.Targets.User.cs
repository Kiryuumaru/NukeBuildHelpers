using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.DependencyModel;
using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Models;
using Octokit;
using Semver;
using Serilog;
using Serilog.Events;
using System.Reflection;
using System.Text.Json;
using YamlDotNet.Core.Tokens;

namespace NukeBuildHelpers;

partial class BaseNukeBuildHelpers
{

    public Target Fetch => _ => _
        .Description("Fetch git commits and tags")
        .Executes(() =>
        {
            Log.Information("Fetching...");
            Git.Invoke($"fetch --prune --prune-tags --force", logInvocation: false, logOutput: false);
            Log.Information("Tags: {Value}", Git.Invoke("tag -l", logInvocation: false, logOutput: false).Select(i => i.Text));
        });

    public Target DeleteOriginTags => _ => _
        .Description("Delete all origin tags, with --args \"{appid}\"")
        .Executes(() =>
        {
            List<string> tagsToDelete = new();
            if (string.IsNullOrEmpty(Args))
            {
                string basePeel = "refs/tags/";
                foreach (var refs in Git.Invoke("ls-remote -t -q", logOutput: false, logInvocation: false))
                {
                    string tag = refs.Text[(refs.Text.IndexOf(basePeel) + basePeel.Length)..];
                    tagsToDelete.Add(tag);
                }
            }
            else
            {
                GetOrFail(() => SplitArgs, out var splitArgs);
                GetOrFail(() => GetAppEntryConfigs(), out var appEntryConfigs);

                foreach (var key in splitArgs.Keys.Any() ? splitArgs.Keys.ToList() : new List<string>() { "" })
                {
                    string appId = key;

                    GetOrFail(appId, appEntryConfigs, out appId, out var appEntry);
                    GetOrFail(() => GetAllVersions(appId, appEntryConfigs), out var allVersions);

                    if (appEntry.Entry.MainRelease)
                    {
                        tagsToDelete.AddRange(allVersions.VersionList.Select(i => i.ToString()));
                    }
                    else
                    {
                        tagsToDelete.AddRange(allVersions.VersionList.Select(i => appId + "/" + i.ToString()));
                    }
                }
            }

            foreach (var tag in tagsToDelete)
            {
                Log.Information("Deleting tag {tag}...", tag);
                Git.Invoke("push origin :refs/tags/" + tag, logInvocation: false, logOutput: false);
            }

            Log.Information("Deleting tag done");
        });

    public Target Version => _ => _
        .Description("Shows the current version from all releases, with --args \"{appid}\"")
        .Executes(() =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntryConfigs);

            Log.Information("Commit: {Value}", Repository.Commit);
            Log.Information("Branch: {Value}", Repository.Branch);

            List<(string Text, HorizontalAlignment Alignment)> headers = new()
                {
                    ("App Id", HorizontalAlignment.Right),
                    ("Environment", HorizontalAlignment.Center),
                    ("Current Version", HorizontalAlignment.Right)
                };
            List<List<string>> rows = new();

            foreach (var key in splitArgs.Keys.Any() ? splitArgs.Keys.ToList() : appEntryConfigs.Select(i => i.Key))
            {
                string appId = key;

                GetOrFail(appId, appEntryConfigs, out appId, out var appEntry);
                GetOrFail(() => GetAllVersions(appId, appEntryConfigs), out var allVersions);

                bool firstEntryRow = true;

                if (allVersions.GroupKeySorted.Any())
                {
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
                        rows.Add(new List<string> { firstEntryRow ? appId : "", env, allVersions.VersionGrouped[groupKey].Last().ToString() });
                        firstEntryRow = false;
                    }
                }
                else
                {
                    rows.Add(new List<string> { appId, null, null });
                }
            }

            LogInfoTable(headers, rows.ToArray());
        });

    public Target Bump => _ => _
        .Description("Bumps the version by tagging and validating tags, with --args \"{appid}={semver}\"")
        .DependsOn(Fetch)
        .Executes(() =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntryConfigs);

            if (!splitArgs.Any())
            {
                Assert.Fail("Args is empty");
                return;
            }

            List<string> tagsToPush = new();

            foreach (var pair in splitArgs.Count > 1 || !string.IsNullOrEmpty(splitArgs.Values.First()) ? splitArgs.ToList() : new List<KeyValuePair<string, string>>() { KeyValuePair.Create("", Args) })
            {
                string appId = pair.Key;
                string versionRaw = pair.Value;

                // ---------- Args validation ----------

                GetOrFail(appId, appEntryConfigs, out appId, out var appEntryConfig);
                GetOrFail(() => GetAllVersions(appId, appEntryConfigs), out var allVersions);

                Log.Information("Validating {appId} bump version {version}...", appId, versionRaw);

                GetOrFail(versionRaw, out SemVersion version);

                // Fail if current branch is not on the proper bump branch
                string envIdentifier;
                string env;
                if (version.IsPrerelease)
                {
                    if (Repository.Branch.ToLowerInvariant() != version.PrereleaseIdentifiers[0])
                    {
                        Assert.Fail($"{version} should bump on {version.PrereleaseIdentifiers[0]} branch");
                        return;
                    }
                    envIdentifier = version.PrereleaseIdentifiers[0];
                    env = version.PrereleaseIdentifiers[0];
                }
                else
                {
                    if (Repository.Branch.ToLowerInvariant() != "master" &&
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

                if (appEntryConfig.Entry.MainRelease)
                {
                    tagsToPush.Add(versionRaw);
                }
                else
                {
                    tagsToPush.Add(appId + "/" + versionRaw);
                }
            }

            foreach (var tag in tagsToPush)
            {
                Git.Invoke($"tag {tag}", logInvocation: false, logOutput: false);
            }

            // ---------- Apply bump ----------

            Log.Information("Pushing bump...");
            Git.Invoke("push origin " + tagsToPush.Select(t => "refs/tags/" + t).Join(" "), logInvocation: false, logOutput: false);
            Log.Information("Bump done");
        });

    public Target Prepare => _ => _
        .Description("Prepare project, with --args \"{appid}\"")
        .Executes(async () =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntries);

            List<Task> tasks = new();

            foreach (var appEntry in appEntries)
            {
                tasks.Add(Task.Run(() => appEntry.Value.Entry.PrepareCore(this, OutputPath)));
                foreach (var appEntryTest in appEntry.Value.Tests)
                {
                    tasks.Add(Task.Run(() => appEntryTest.PrepareCore(this)));
                }
            }

            await Task.WhenAll(tasks);
        });

    public Target Test => _ => _
        .Description("Test, with --args \"{appid}\"")
        .DependsOn(Prepare)
        .Executes(async () =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntries);

            List<Task> tasks = new();

            foreach (var appEntry in appEntries)
            {
                foreach (var appEntryTest in appEntry.Value.Tests)
                {
                    tasks.Add(Task.Run(() => appEntryTest.RunCore(this)));
                }
            }

            await Task.WhenAll(tasks);
        });

    public Target Build => _ => _
        .Description("Build, with --args \"{appid}\"")
        .DependsOn(Test)
        .Executes(async () =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntries);

            List<Task> tasks = new();

            foreach (var appEntry in appEntries)
            {
                tasks.Add(Task.Run(() => appEntry.Value.Entry.BuildCore(this, OutputPath)));
            }

            await Task.WhenAll(tasks);
        });

    public Target Pack => _ => _
        .Description("Pack, with --args \"{appid}\"")
        .DependsOn(Build)
        .Executes(async () =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntries);

            List<Task> tasks = new();

            foreach (var appEntry in appEntries)
            {
                tasks.Add(Task.Run(() => appEntry.Value.Entry.PackCore(this, OutputPath)));
            }

            await Task.WhenAll(tasks);
        });

    public Target Release => _ => _
        .Description("Release, with --args \"{appid}\"")
        .DependsOn(Pack)
        .Executes(async () =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntries);

            List<Task> tasks = new();

            foreach (var appEntry in appEntries)
            {
                tasks.Add(Task.Run(() => appEntry.Value.Entry.ReleaseCore(this, OutputPath)));
            }

            await Task.WhenAll(tasks);
        });
}
