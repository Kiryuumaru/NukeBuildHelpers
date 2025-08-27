using Nuke.Common;
using Nuke.Common.Tooling;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Common.Models;
using NukeBuildHelpers.ConsoleInterface;
using NukeBuildHelpers.ConsoleInterface.Enums;
using NukeBuildHelpers.ConsoleInterface.Models;
using NukeBuildHelpers.Entry.Helpers;
using NukeBuildHelpers.Pipelines.Azure;
using NukeBuildHelpers.Pipelines.Common;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.Pipelines.Github;
using Serilog;

namespace NukeBuildHelpers;

partial class BaseNukeBuildHelpers
{
    /// <summary>
    /// Fetches git commits and tags.
    /// </summary>
    public Target Fetch => _ => _
        .Description("Fetch git commits and tags")
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            await RunFetch();
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

            await RunVersion();
        });

    /// <summary>
    /// Interactive for selecting and running specific tasks.
    /// </summary>
    public Target Interactive => _ => _
        .Description("Interactive for selecting and running specific tasks")
        .Executes(async () =>
        {
            await RunInteractive();
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

            await StartStatusWatch(true, [.. bumpMap.Select(i => (i.Key, Repository.Branch))]);
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

            var pipeline = await PipelineHelpers.SetupPipeline(this);

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
                    await TestAppEntries(allEntry, pipeline, testAppEntries);
                }
                if (buildAppEntries.Any())
                {
                    await BuildAppEntries(allEntry, pipeline, buildAppEntries);
                }
                if (publishAppEntries.Any())
                {
                    await PublishAppEntries(allEntry, pipeline, publishAppEntries);
                }
            }
            else
            {
                await TestAppEntries(allEntry, pipeline, []);
                await BuildAppEntries(allEntry, pipeline, []);
                await PublishAppEntries(allEntry, pipeline, []);
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

            var pipeline = await PipelineHelpers.SetupPipeline(this);

            await TestAppEntries(allEntry, pipeline, splitArgs.Select(i => i.Key));
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

            var pipeline = await PipelineHelpers.SetupPipeline(this);

            await BuildAppEntries(allEntry, pipeline, splitArgs.Select(i => i.Key));
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

            var pipeline = await PipelineHelpers.SetupPipeline(this);

            await PublishAppEntries(allEntry, pipeline, splitArgs.Select(i => i.Key));
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
