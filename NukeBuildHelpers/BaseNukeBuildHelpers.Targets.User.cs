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
        .Description("Shows the current version from all releases")
        .DependsOn(Fetch)
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            var allEntry = await ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this));

            CheckAppEntry(allEntry);

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

            foreach (var key in allEntry.AppEntryMap.Select(i => i.Key))
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
            var bumpMap = await RunBumpArgsOrInteractive();

            Console.WriteLine();

            await StartStatusWatch(true, bumpMap.Select(i => (i.Key, Repository.Branch)).ToArray());
        });

    /// <summary>
    /// Bumps and forgets the version by validating and tagging.
    /// </summary>
    public Target BumpAndForget => _ => _
        .Description("Bumps and forget the version by validating and tagging, with optional --args \"{appid=[major|minor|patch|prerelease|pre][>|+]int}\"")
        .DependsOn(Version)
        .Executes(async () =>
        {
            await RunBumpArgsOrInteractive();
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
    /// Run the entry, with --args "{idsToRun}".
    /// </summary>
    public Target Run => _ => _
        .Description("Run, with --args \"{idsToRun}\"")
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            ValueHelpers.GetOrFail(() => SplitArgs, out var splitArgs);

            var allEntry = await ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this));

            CheckAppEntry(allEntry);

            var pipeline = PipelineHelpers.SetupPipeline(this);

            var idsToRun = splitArgs.Select(i => i.Key);

            if (idsToRun.Any())
            {
                foreach (var id in idsToRun)
                {
                    if (!allEntry.RunEntryDefinitionMap.Any(i => i.Value.Id.Equals(id, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        throw new ArgumentException($"Id {id} does not exists");
                    }
                }

                var testAppEntries = allEntry.TestEntryDefinitionMap.Values.Where(i => idsToRun.Any(j => j.Equals(i.Id, StringComparison.InvariantCultureIgnoreCase))).Select(i => i.Id);
                var buildAppEntries = allEntry.BuildEntryDefinitionMap.Values.Where(i => idsToRun.Any(j => j.Equals(i.Id, StringComparison.InvariantCultureIgnoreCase))).Select(i => i.Id);
                var publishAppEntries = allEntry.PublishEntryDefinitionMap.Values.Where(i => idsToRun.Any(j => j.Equals(i.Id, StringComparison.InvariantCultureIgnoreCase))).Select(i => i.Id);

                if (testAppEntries.Any())
                {
                    await TestAppEntries(allEntry, pipeline, testAppEntries, true);
                }
                if (buildAppEntries.Any())
                {
                    await BuildAppEntries(allEntry, pipeline, buildAppEntries, true);
                }
                if (publishAppEntries.Any())
                {
                    await PublishAppEntries(allEntry, pipeline, publishAppEntries, true);
                }
            }
            else
            {
                await TestAppEntries(allEntry, pipeline, [], true);
                await BuildAppEntries(allEntry, pipeline, [], true);
                await PublishAppEntries(allEntry, pipeline, [], true);
            }
        });

    /// <summary>
    /// Tests the application, with --args "{idsToRun}".
    /// </summary>
    public Target Test => _ => _
        .Description("Test, with --args \"{idsToRun}\"")
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            ValueHelpers.GetOrFail(() => SplitArgs, out var splitArgs);

            var allEntry = await ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this));

            CheckAppEntry(allEntry);

            var pipeline = PipelineHelpers.SetupPipeline(this);

            await TestAppEntries(allEntry, pipeline, splitArgs.Select(i => i.Key), true);
        });

    /// <summary>
    /// Builds the application, with --args "{idsToRun}".
    /// </summary>
    public Target Build => _ => _
        .Description("Build, with --args \"{idsToRun}\"")
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            ValueHelpers.GetOrFail(() => SplitArgs, out var splitArgs);

            var allEntry = await ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this));

            CheckAppEntry(allEntry);

            var pipeline = PipelineHelpers.SetupPipeline(this);

            await BuildAppEntries(allEntry, pipeline, splitArgs.Select(i => i.Key), true);
        });

    /// <summary>
    /// Publishes the application, with --args "{idsToRun}".
    /// </summary>
    public Target Publish => _ => _
        .Description("Publish, with --args \"{idsToRun}\"")
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            ValueHelpers.GetOrFail(() => SplitArgs, out var splitArgs);

            var allEntry = await ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this));

            CheckAppEntry(allEntry);

            var pipeline = PipelineHelpers.SetupPipeline(this);

            await PublishAppEntries(allEntry, pipeline, splitArgs.Select(i => i.Key), true);
        });

    /// <summary>
    /// Builds the CICD workflow for GitHub.
    /// </summary>
    public Target GithubWorkflow => _ => _
        .Description("Builds the cicd workflow for github")
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            var allEntry = await ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this));

            CheckAppEntry(allEntry);

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

            var allEntry = await ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this));

            CheckAppEntry(allEntry);

            await PipelineHelpers.BuildWorkflow<AzurePipeline>(this, allEntry);
        });
}
