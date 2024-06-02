using Nuke.Common;
using Nuke.Common.Tooling;
using NukeBuildHelpers.Enums;
using Serilog;

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
            GetOrFail(() => GetAppConfig(), out var appConfig);

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

            foreach (var key in splitArgs.Keys.Any() ? splitArgs.Keys.ToList() : appConfig.AppEntryConfigs.Select(i => i.Key))
            {
                string appId = key;

                GetOrFail(appId, appConfig.AppEntryConfigs, out appId, out var appEntry);
                GetOrFail(() => GetAllVersions(appId, appConfig.AppEntryConfigs, ref lsRemote), out var allVersions);

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
                        var bumpedVersionStr = IsVersionEmpty(bumpedVersion) ? "-" : bumpedVersion.ToString();
                        rows.Add([firstEntryRow ? appId : "", env, bumpedVersionStr, published]);
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

    public Target Release => _ => _
        .Description("Release the version by validating and create a release pull request")
        .DependsOn(Version)
        .Executes(async () =>
        {
            var appEntryVersionsToBump = await StartBump();

            Console.WriteLine();

            await StartStatusWatch(true, appEntryVersionsToBump.Select(i => (i.AppEntry.Id, Repository.Branch.ToLowerInvariant())).ToArray());
        });

    public Target Bump => _ => _
        .Description("Bumps the version by validating and tagging")
        .DependsOn(Version)
        .Executes(async () =>
        {
            var appEntryVersionsToBump = await StartBump();

            Console.WriteLine();

            await StartStatusWatch(true, appEntryVersionsToBump.Select(i => (i.AppEntry.Id, Repository.Branch.ToLowerInvariant())).ToArray());
        });

    public Target BumpAndForget => _ => _
        .Description("Bumps and forget the version by validating and tagging")
        .DependsOn(Version)
        .Executes(async () =>
        {
            await StartBump();
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
            GetOrFail(() => GetAppConfig(), out var appConfig);

            await TestAppEntries(appConfig, splitArgs.Select(i => i.Key), null);
        });

    public Target Build => _ => _
        .Description("Build, with --args \"{appid}\"")
        .DependsOn(Test)
        .Executes(async () =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppConfig(), out var appConfig);

            await BuildAppEntries(appConfig, splitArgs.Select(i => i.Key), null);
        });

    public Target Publish => _ => _
        .Description("Publish, with --args \"{appid}\"")
        .DependsOn(Build)
        .Executes(async () =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppConfig(), out var appConfig);

            await PublishAppEntries(appConfig, splitArgs.Select(i => i.Key), null);
        });

    public Target GithubWorkflow => _ => _
        .Description("Builds the cicd workflow for github")
        .Executes(BuildWorkflow<GithubPipeline>);

    public Target AzureWorkflow => _ => _
        .Description("Builds the cicd workflow for azure")
        .Executes(BuildWorkflow<AzurePipeline>);
}
