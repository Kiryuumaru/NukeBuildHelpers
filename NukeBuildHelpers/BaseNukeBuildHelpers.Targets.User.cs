using Nuke.Common;
using Nuke.Common.Tooling;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.ConsoleInterface;
using NukeBuildHelpers.ConsoleInterface.Enums;
using NukeBuildHelpers.ConsoleInterface.Models;
using NukeBuildHelpers.Entry.Helpers;
using NukeBuildHelpers.Pipelines.Azure;
using NukeBuildHelpers.Pipelines.Common;
using NukeBuildHelpers.Pipelines.Github;
using Serilog;

namespace NukeBuildHelpers;

partial class BaseNukeBuildHelpers
{
    public Target Fetch => _ => _
        .Description("Fetch git commits and tags")
        .Executes(() =>
        {
            CheckEnvironementBranches();

            Log.Information("Fetching...");
            Git.Invoke("fetch --prune --prune-tags --force", logInvocation: false, logOutput: false);
        });

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

    public Target Bump => _ => _
        .Description("Bumps the version by validating and tagging")
        .DependsOn(Version)
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            var appEntryVersionsToBump = await StartBump();

            Console.WriteLine();

            await StartStatusWatch(true, appEntryVersionsToBump.Select(i => (i.AppEntry.AppId, Repository.Branch)).ToArray());
        });

    public Target BumpAndForget => _ => _
        .Description("Bumps and forget the version by validating and tagging")
        .DependsOn(Version)
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            await StartBump();
        });

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

    public Target Test => _ => _
        .Description("Test, with --args \"{appid}\"")
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            ValueHelpers.GetOrFail(() => SplitArgs, out var splitArgs);
            ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this), out var allEntry);

            await TestAppEntries(allEntry, splitArgs.Select(i => i.Key));
        });

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

    public Target GithubWorkflow => _ => _
        .Description("Builds the cicd workflow for github")
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this), out var allEntry);

            await PipelineHelpers.BuildWorkflow<GithubPipeline>(this, allEntry);
        });

    public Target AzureWorkflow => _ => _
        .Description("Builds the cicd workflow for azure")
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this), out var allEntry);

            await PipelineHelpers.BuildWorkflow<AzurePipeline>(this, allEntry);
        });
}
