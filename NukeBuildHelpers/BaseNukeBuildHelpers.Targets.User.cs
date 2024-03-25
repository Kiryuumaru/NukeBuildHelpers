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
using Sharprompt;
using System.ComponentModel.DataAnnotations;
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
            Git.Invoke("fetch --prune --prune-tags --force", logInvocation: false, logOutput: false);
        });

    public Target Version => _ => _
        .Description("Shows the current version from all releases, with --args \"{appid}\"")
        .DependsOn(Fetch)
        .Executes(() =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntryConfigs);

            Log.Information("Commit: {Value}", Repository.Commit);
            Log.Information("Branch: {Value}", Repository.Branch);

            List<(string Text, HorizontalAlignment Alignment)> headers =
                [
                    ("App Id", HorizontalAlignment.Right),
                    ("Environment", HorizontalAlignment.Center),
                    ("Bumped Version", HorizontalAlignment.Right),
                    ("Published", HorizontalAlignment.Center)
                ];
            List<List<string?>> rows = [];

            Console.WriteLine();

            IReadOnlyCollection<Output>? lsRemote = null;

            foreach (var key in splitArgs.Keys.Any() ? splitArgs.Keys.ToList() : appEntryConfigs.Select(i => i.Key))
            {
                string appId = key;

                GetOrFail(appId, appEntryConfigs, out appId, out var appEntry);
                GetOrFail(() => GetAllVersions(appId, appEntryConfigs, ref lsRemote), out var allVersions);

                bool firstEntryRow = true;

                if (allVersions.EnvSorted.Count != 0)
                {
                    foreach (var groupKey in allVersions.EnvSorted)
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
                        var bumpedVersion = allVersions.EnvVersionGrouped[groupKey].Last();
                        allVersions.EnvLatestVersionPaired.TryGetValue(groupKey, out var releasedVersion);
                        var published = "yes";
                        if (releasedVersion == null)
                        {
                            published = "no";
                        }
                        else if (bumpedVersion != releasedVersion)
                        {
                            published = releasedVersion + "*";
                        }
                        rows.Add([firstEntryRow ? appId : "", env, bumpedVersion.ToString(), published]);
                        firstEntryRow = false;
                    }
                }
                else
                {
                    rows.Add([appId, null, null, "no"]);
                }
                rows.Add(["-", "-", "-", "-"]);
            }
            rows.RemoveAt(rows.Count - 1);

            LogInfoTable(headers, [.. rows]);
        });

    public Target Bump => _ => _
        .Description("Bumps the version by tagging and validating tags")
        .DependsOn(Version)
        .Executes(async () =>
        {
            Prompt.ColorSchema.Answer = ConsoleColor.Green;
            Prompt.ColorSchema.Select = ConsoleColor.DarkMagenta;
            Prompt.Symbols.Prompt = new Symbol("?", "?");
            Prompt.Symbols.Done = new Symbol("✓", "✓");
            Prompt.Symbols.Error = new Symbol("x", "x");

            if (!EnvironmentBranches.Any(i => i.Equals(Repository.Branch, StringComparison.InvariantCultureIgnoreCase)))
            {
                Assert.Fail($"{Repository.Branch} is not on environment branches");
            }

            GetOrFail(() => GetAppEntryConfigs(), out var appEntryConfigs);

            IReadOnlyCollection<Output>? lsRemote = null;

            List<(AppEntry? AppEntry, AllVersions? AllVersions)> appEntryVersions = [];

            foreach (var pair in appEntryConfigs)
            {
                string appId = pair.Key;

                GetOrFail(appId, appEntryConfigs, out appId, out var appEntryConfig);
                GetOrFail(() => GetAllVersions(appId, appEntryConfigs, ref lsRemote), out var allVersions);

                appEntryVersions.Add((pair.Value.Entry, allVersions));
            }

            appEntryVersions.Add((null, null));

            List<(AppEntry AppEntry, AllVersions AllVersions, SemVersion BumpVersion)> appEntryVersionsToBump = [];

            while (true)
            {
                var appEntryVersion = Prompt.Select("App id to bump",
                    appEntryVersions.Where(i => !appEntryVersionsToBump.Any(j => j.AppEntry.Id == i.AppEntry?.Id)),
                    textSelector: (appEntry) => appEntry.AppEntry == null ? "->done" : appEntry.AppEntry.Id);

                if (appEntryVersion.AppEntry == null || appEntryVersion.AllVersions == null)
                {
                    if (appEntryVersionsToBump.Count != 0)
                    {
                        var answer = Prompt.Confirm("Are you sure to bump selected version(s)?", defaultValue: true);
                        if (answer)
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                    continue;
                }

                string currentEnvIdentifier;
                if (Repository.Branch.Equals(MainEnvironmentBranch, StringComparison.InvariantCultureIgnoreCase))
                {
                    currentEnvIdentifier = "";
                }
                else
                {
                    currentEnvIdentifier = Repository.Branch.ToLowerInvariant();
                }
                appEntryVersion.AllVersions.EnvVersionGrouped.TryGetValue(currentEnvIdentifier, out var currentEnvLatestVersion);
                var currColor = Console.ForegroundColor;
                Console.Write("  Current latest version: ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(currentEnvLatestVersion?.LastOrDefault()?.ToString() ?? "null");
                Console.ForegroundColor = currColor;
                Console.WriteLine("");
                var bumpVersionStr = Prompt.Input<string>("New Version", validators: [Validators.Required(),
                    (input => {
                        if (!SemVersion.TryParse(input.ToString(), SemVersionStyles.Strict, out var inputVersion))
                        {
                            return new ValidationResult("Invalid semver version");
                        }
                        
                        // Fail if current branch is not on the proper bump branch
                        string envIdentifier;
                        string env;
                        if (inputVersion.IsPrerelease)
                        {
                            if (!Repository.Branch.Equals(inputVersion.PrereleaseIdentifiers[0], StringComparison.InvariantCultureIgnoreCase))
                            {
                                return new ValidationResult($"{inputVersion} should bump on {inputVersion.PrereleaseIdentifiers[0]} branch");
                            }
                            envIdentifier = inputVersion.PrereleaseIdentifiers[0];
                            env = inputVersion.PrereleaseIdentifiers[0];
                        }
                        else
                        {
                            if (!Repository.Branch.Equals(MainEnvironmentBranch, StringComparison.InvariantCultureIgnoreCase))
                            {
                                return new ValidationResult($"{inputVersion} should bump on main branch");
                            }
                            envIdentifier = "";
                            env = "main";
                        }

                        if (appEntryVersion.AllVersions.EnvVersionGrouped.TryGetValue(envIdentifier, out List<SemVersion>? value))
                        {
                            var lastVersion = value.Last();
                            // Fail if the version is already released
                            if (SemVersion.ComparePrecedence(lastVersion, inputVersion) == 0)
                            {
                                return new ValidationResult($"The latest version in the {env} releases is already {inputVersion}");
                            }
                            // Fail if the version is behind the latest release
                            if (SemVersion.ComparePrecedence(lastVersion, inputVersion) > 0)
                            {
                                return new ValidationResult($"{inputVersion} is behind the latest version {lastVersion} in the {env} releases");
                            }
                        }

                        return ValidationResult.Success;
                    })]);
                var bumpVersion = SemVersion.Parse(bumpVersionStr, SemVersionStyles.Strict);
                appEntryVersionsToBump.Add((appEntryVersion.AppEntry, appEntryVersion.AllVersions, bumpVersion));
            }

            if (appEntryVersionsToBump.Count == 0)
            {
                Log.Information("No version selected to bump.");
                return;
            }

            List<string> tagsToPush = [];

            foreach (var appEntryVersionToBump in appEntryVersionsToBump)
            {
                if (appEntryVersionToBump.AppEntry.MainRelease)
                {
                    tagsToPush.Add(appEntryVersionToBump.BumpVersion.ToString());
                }
                else
                {
                    tagsToPush.Add(appEntryVersionToBump.AppEntry.Id.ToLowerInvariant() + "/" + appEntryVersionToBump.BumpVersion.ToString());
                }
            }

            foreach (var tag in tagsToPush)
            {
                Git.Invoke($"tag {tag}", logInvocation: false, logOutput: false);
            }

            // ---------- Apply bump ----------

            Git.Invoke("push origin HEAD", logInvocation: false, logOutput: false);
            Git.Invoke("push origin " + tagsToPush.Select(t => "refs/tags/" + t).Join(" "), logInvocation: false, logOutput: false);

            var bumpTag = "bump-" + Repository.Branch.ToLowerInvariant();
            try
            {
                Git.Invoke("push origin :refs/tags/" + bumpTag, logInvocation: false, logOutput: false);
            }
            catch { }
            Git.Invoke("tag --force " + bumpTag, logInvocation: false, logOutput: false);
            Git.Invoke("push origin --force " + bumpTag, logInvocation: false, logOutput: false);

            Console.WriteLine();

            await StartStatusWatch(true);
        });

    public Target StatusWatch => _ => _
        .Description("Shows the current version from all releases, with --args \"{appid}\"")
        .Executes(async () =>
        {
            Log.Information("Commit: {Value}", Repository.Commit);
            Log.Information("Branch: {Value}", Repository.Branch);

            Console.WriteLine();

            await StartStatusWatch(false);
        });

    public Target Test => _ => _
        .Description("Test, with --args \"{appid}\"")
        .Executes(async () =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntries);

            await TestAppEntries(appEntries, splitArgs.Select(i => i.Key), null);
        });

    public Target Build => _ => _
        .Description("Build, with --args \"{appid}\"")
        .DependsOn(Test)
        .Executes(async () =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntries);

            await BuildAppEntries(appEntries, splitArgs.Select(i => i.Key), null);
        });

    public Target Publish => _ => _
        .Description("Publish, with --args \"{appid}\"")
        .DependsOn(Build)
        .Executes(async () =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntries);

            await PublishAppEntries(appEntries, splitArgs.Select(i => i.Key), null);
        });

    public Target GithubWorkflow => _ => _
        .Description("Builds the cicd workflow for github")
        .Executes(BuildWorkflow<GithubPipeline>);

    public Target AzureWorkflow => _ => _
        .Description("Builds the cicd workflow for azure")
        .Executes(BuildWorkflow<AzurePipeline>);
}
