using Nuke.Common;
using Nuke.Common.Tooling;
using Nuke.Common.Utilities;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.ConsoleInterface;
using NukeBuildHelpers.ConsoleInterface.Enums;
using NukeBuildHelpers.ConsoleInterface.Models;
using NukeBuildHelpers.Entry.Helpers;
using NukeBuildHelpers.Pipelines.Azure;
using NukeBuildHelpers.Pipelines.Common;
using NukeBuildHelpers.Pipelines.Github;
using Semver;
using Serilog;

namespace NukeBuildHelpers;

partial class BaseNukeBuildHelpers
{
    /// <summary>
    /// Fetches git commits and tags.
    /// </summary>
    public Target Fetch => _ => _
        .Description("Fetch git commits and tags")
        .Executes(() =>
        {
            CheckEnvironementBranches();

            Log.Information("Fetching...");
            Git.Invoke("fetch --prune --prune-tags --force", logInvocation: false, logOutput: false);
        });

    /// <summary>
    /// Shows the current version from all releases, with --args "{appid}".
    /// </summary>
    public Target Version => _ => _
        .Description("Shows the current version from all releases, with --args \"{appid}\"")
        .DependsOn(Fetch)
        .Executes(() =>
        {
            CheckEnvironementBranches();

            ValueHelpers.GetOrFail(() => SplitArgs, out var splitArgs);
            ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this), out var allEntry);

            Log.Information("Commit: {Value}", Repository.Commit);
            Log.Information("Branch: {Value}", Repository.Branch);

            ConsoleTableHeader[] headers =
            [
                ("App EntryId", HorizontalAlignment.Right),
                ("Environment", HorizontalAlignment.Center),
                ("Bumped Version", HorizontalAlignment.Right),
                ("Published", HorizontalAlignment.Center)
            ];
            List<ConsoleTableRow> rows = [];

            IReadOnlyCollection<Output>? lsRemote = null;

            foreach (var key in splitArgs.Keys.Any() ? splitArgs.Keys.ToList() : allEntry.AppEntryMap.Select(i => i.Key))
            {
                string appId = key;

                ValueHelpers.GetOrFail(appId, allEntry, out var appEntry);
                ValueHelpers.GetOrFail(() => EntryHelpers.GetAllVersions(this, appId, ref lsRemote), out var allVersions);

                bool firstEntryRow = true;

                if (allVersions.EnvSorted.Count != 0)
                {
                    foreach (var env in allVersions.EnvSorted)
                    {
                        var bumpedVersion = allVersions.EnvVersionGrouped[env].Last();
                        allVersions.EnvLatestVersionPaired.TryGetValue(env, out var releasedVersion);
                        var published = "yes";
                        if (releasedVersion == null)
                        {
                            published = "no";
                        }
                        else if (bumpedVersion != releasedVersion)
                        {
                            published = releasedVersion + "*";
                        }
                        var bumpedVersionStr = SemverHelpers.IsVersionEmpty(bumpedVersion) ? "-" : bumpedVersion.ToString();
                        rows.Add(ConsoleTableRow.FromValue([firstEntryRow ? appId : "", env, bumpedVersionStr, published]));
                        firstEntryRow = false;
                    }
                }
                else
                {
                    rows.Add(ConsoleTableRow.FromValue([appId, default(string), default(string), "no"]));
                }
                rows.Add(ConsoleTableRow.Separator);
            }
            rows.RemoveAt(rows.Count - 1);

            Console.WriteLine();

            ConsoleTableHelpers.LogInfoTable(headers, [.. rows]);
        });

    /// <summary>
    /// Bumps the version by validating and tagging.
    /// </summary>
    public Target Bump => _ => _
        .Description("Bumps the version by validating and tagging")
        .DependsOn(Version)
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this), out var allEntry);

            var appEntryVersionsToBump = await StartBump(allEntry);

            Console.WriteLine();

            await StartStatusWatch(true, appEntryVersionsToBump.Select(i => (i.AppEntry.AppId, Repository.Branch)).ToArray());
        });

    /// <summary>
    /// Bumps and forgets the version by validating and tagging.
    /// </summary>
    public Target BumpAndForget => _ => _
        .Description("Bumps and forget the version by validating and tagging, with optional --args \"{appid=[major|minor|patch|prerelease|pre][+|>]int}\"")
        .DependsOn(Version)
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            ValueHelpers.GetOrFail(() => SplitArgs, out var splitArgs);
            ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this), out var allEntry);

            string currentEnvIdentifier = Repository.Branch.ToLowerInvariant();

            IReadOnlyCollection<Output>? lsRemote = null;

            Dictionary<string, SemVersion> bumpMap = [];
            foreach (var splitArg in splitArgs)
            {
                ValueHelpers.GetOrFail(splitArg.Key, allEntry, out var appEntry);
                ValueHelpers.GetOrFail(() => EntryHelpers.GetAllVersions(this, splitArg.Key, ref lsRemote), out var allVersions);

                SemVersion latestVersion = allVersions.EnvVersionGrouped[currentEnvIdentifier]?.LastOrDefault()!;
                SemVersion bumpVersion = latestVersion.Clone();

                foreach (var bumpPart in splitArg.Value.NotNullOrEmpty().Trim().ToLowerInvariant().Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    try
                    {
                        bool isIncrement = true;
                        string[] bumpValue = [];
                        if (bumpPart.Contains('+'))
                        {
                            isIncrement = true;
                            bumpValue = bumpPart.Split("+");
                        }
                        else if (bumpPart.Contains('>'))
                        {
                            isIncrement = false;
                            bumpValue = bumpPart.Split(">");
                        }
                        else
                        {
                            bumpValue = [bumpPart];
                        }
                        int bumpAssign = bumpValue.Length > 1 ? int.Parse(bumpValue[1]) : 1;
                        switch (bumpValue[0])
                        {
                            case "major":
                                bumpVersion = bumpVersion.WithMajor(isIncrement ? bumpVersion.Major + bumpAssign : bumpAssign);
                                break;
                            case "minor":
                                bumpVersion = bumpVersion.WithMinor(isIncrement ? bumpVersion.Minor + bumpAssign : bumpAssign);
                                break;
                            case "patch":
                                bumpVersion = bumpVersion.WithPatch(isIncrement ? bumpVersion.Patch + bumpAssign : bumpAssign);
                                break;
                            case "prerelease":
                            case "pre":
                                var prereleaseSplit = bumpVersion.Prerelease.Split(".");
                                bumpVersion = bumpVersion.WithPrereleaseParsedFrom(prereleaseSplit[0] + "." + (isIncrement ? int.Parse(prereleaseSplit[1]) + bumpAssign : bumpAssign));
                                break;
                            default:
                                throw new ArgumentException("Invalid bump value " + splitArg.Value);
                        }
                    }
                    catch
                    {
                        throw new ArgumentException("Invalid bump value " + splitArg.Value);
                    }
                }

                ValidateBumpVersion(allVersions, bumpVersion.ToString());

                bumpMap[splitArg.Key] = bumpVersion;

                Log.Information("Bump {appId} from {latestVersion} to {bumpVersion}", splitArg.Key, latestVersion, bumpVersion);
            }

            if (bumpMap.Count == 0)
            {
                await StartBump(allEntry);
            }
            else
            {
                await RunBump(allEntry, bumpMap);
            }
        });

    /// <summary>
    /// Shows the current version from all releases, with --args "{appid}".
    /// </summary>
    public Target StatusWatch => _ => _
        .Description("Shows the current version from all releases, with --args \"{appid}\"")
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            Log.Information("Commit: {Value}", Repository.Commit);
            Log.Information("Branch: {Value}", Repository.Branch);

            Console.WriteLine();

            await StartStatusWatch(false);
        });

    /// <summary>
    /// Tests the application, with --args "{appid}".
    /// </summary>
    public Target Test => _ => _
        .Description("Test, with --args \"{appid}\"")
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            ValueHelpers.GetOrFail(() => SplitArgs, out var splitArgs);
            ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this), out var allEntry);

            await TestAppEntries(allEntry, splitArgs.Select(i => i.Key));
        });

    /// <summary>
    /// Builds the application, with --args "{appid}".
    /// </summary>
    public Target Build => _ => _
        .Description("Build, with --args \"{appid}\"")
        .DependsOn(Test)
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            ValueHelpers.GetOrFail(() => SplitArgs, out var splitArgs);
            ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this), out var allEntry);

            await BuildAppEntries(allEntry, splitArgs.Select(i => i.Key));
        });

    /// <summary>
    /// Publishes the application, with --args "{appid}".
    /// </summary>
    public Target Publish => _ => _
        .Description("Publish, with --args \"{appid}\"")
        .DependsOn(Build)
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            ValueHelpers.GetOrFail(() => SplitArgs, out var splitArgs);
            ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this), out var allEntry);

            await PublishAppEntries(allEntry, splitArgs.Select(i => i.Key));
        });

    /// <summary>
    /// Builds the CICD workflow for GitHub.
    /// </summary>
    public Target GithubWorkflow => _ => _
        .Description("Builds the cicd workflow for github")
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this), out var allEntry);

            await PipelineHelpers.BuildWorkflow<GithubPipeline>(this, allEntry);
        });

    /// <summary>
    /// Builds the CICD workflow for Azure.
    /// </summary>
    public Target AzureWorkflow => _ => _
        .Description("Builds the cicd workflow for azure")
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this), out var allEntry);

            await PipelineHelpers.BuildWorkflow<AzurePipeline>(this, allEntry);
        });
}
