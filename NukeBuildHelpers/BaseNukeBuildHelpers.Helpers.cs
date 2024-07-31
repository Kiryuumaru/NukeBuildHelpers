using Nuke.Common;
using Nuke.Common.IO;
using Semver;
using Serilog;
using NukeBuildHelpers.Common;
using Nuke.Common.Tooling;
using Nuke.Common.Utilities;
using Sharprompt;
using System.ComponentModel.DataAnnotations;
using NukeBuildHelpers.ConsoleInterface;
using NukeBuildHelpers.ConsoleInterface.Models;
using System.Text.Json;
using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.ConsoleInterface.Enums;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;
using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.Pipelines.Common.Models;
using NukeBuildHelpers.Pipelines.Common.Enums;
using NukeBuildHelpers.Pipelines.Common;
using NukeBuildHelpers.Entry.Helpers;
using NukeBuildHelpers.RunContext.Extensions;

namespace NukeBuildHelpers;

partial class BaseNukeBuildHelpers
{
    private readonly string artifactNameSeparator = "___";

    private static readonly AbsolutePath entryCachePath = CommonCacheDirectory / "entry";
    private static readonly AbsolutePath entryCacheIndexPath = CommonCacheDirectory / "entry_index";

    private void CheckEnvironementBranches()
    {
        HashSet<string> set = [];

        foreach (string env in EnvironmentBranches.Select(i => i.ToLowerInvariant()))
        {
            if (!set.Add(env))
            {
                throw new Exception($"Duplicate environment branch \"{env}\"");
            }
        }

        if (!set.Contains(MainEnvironmentBranch.ToLowerInvariant()))
        {
            throw new Exception($"MainEnvironmentBranch \"{MainEnvironmentBranch}\" does not exists in EnvironmentBranches");
        }
    }

    private static void CheckAppEntry(AllEntry allEntry)
    {
        if (allEntry.AppEntryMap.Count == 0)
        {
            throw new Exception($"No configured app entry.");
        }
    }

    private static void CacheBump()
    {
        if (!CommonCacheOutputDirectory.DirectoryExists())
        {
            CommonCacheOutputDirectory.CreateDirectory();
        }

        (CommonCacheDirectory / "stamp").WriteAllText(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
    }

    private static void OutputBump()
    {
        if (!CommonOutputDirectory.DirectoryExists())
        {
            CommonOutputDirectory.CreateDirectory();
        }

        (CommonOutputDirectory / "stamp").WriteAllText(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
    }

    private static Dictionary<string, AbsolutePath> GetCacheIndex()
    {
        Dictionary<string, AbsolutePath> cachePairs = [];

        if (entryCacheIndexPath.FileExists())
        {
            try
            {
                var cachePairsStr = JsonSerializer.Deserialize<Dictionary<string, string>>(entryCacheIndexPath.ReadAllText()) ?? [];
                foreach (var pair in cachePairsStr)
                {
                    try
                    {
                        cachePairs[pair.Key] = pair.Value;
                    }
                    catch { }
                }
            }
            catch { }
        }

        return cachePairs;
    }

    private static void SetCacheIndex(Dictionary<string, AbsolutePath> cacheIndex)
    {
        try
        {
            if (!entryCacheIndexPath.Parent.DirectoryExists())
            {
                entryCacheIndexPath.Parent.CreateDirectory();
            }
            entryCacheIndexPath.WriteAllText(JsonSerializer.Serialize(cacheIndex.ToDictionary(i => i.Key, i => i.Value.ToString())));
        }
        catch { }
    }

    private static async Task CachePreload(IRunEntryDefinition entry)
    {
        if (!entryCachePath.DirectoryExists())
        {
            entryCachePath.CreateDirectory();
        }

        var cachePaths = entry.CachePath == null ? [] : await entry.GetCachePaths();

        Dictionary<string, AbsolutePath> cachePairs = GetCacheIndex();

        List<Task> tasks = [];

        foreach (var dir in entryCachePath.GetDirectories())
        {
            if (!cachePairs.Any(i => i.Value == dir))
            {
                dir.DeleteDirectory();
                Log.Information("{path} cache cleaned", dir);
            }
        }

        foreach (var pair in cachePairs.Clone())
        {
            if (!cachePaths.Any(i => i == pair.Key))
            {
                cachePairs.Remove(pair.Key);
                pair.Value.DeleteDirectory();
                Log.Information("{path} cache cleaned", pair.Key);
            }
        }

        foreach (var path in cachePaths)
        {
            if (!cachePairs.TryGetValue(path.ToString(), out var cachePath) || !cachePath.DirectoryExists())
            {
                Log.Information("{path} cache missed", path);
                continue;
            }
            tasks.Add(Task.Run(async () =>
            {
                var cachePathValue = cachePath / "currentLatest";
                path.Parent.CreateDirectory();
                if (cachePathValue.FileExists())
                {
                    File.Move(cachePathValue, path, true);
                }
                else if (cachePathValue.DirectoryExists())
                {
                    await cachePathValue.MoveRecursively(path);
                }
                Log.Information("{path} cache loaded", path);
            }));
        }

        await Task.WhenAll(tasks);

        SetCacheIndex(cachePairs);
    }

    private static async Task CachePostload(IRunEntryDefinition entry)
    {
        var cachePaths = entry.CachePath == null ? [] : await entry.GetCachePaths();

        Dictionary<string, AbsolutePath> cachePairs = GetCacheIndex();

        List<Task> tasks = [];

        foreach (var path in cachePaths)
        {
            if (!path.FileExists() && !path.DirectoryExists())
            {
                Log.Information("{path} cache missed", path);
                continue;
            }
            if (!cachePairs.TryGetValue(path.ToString(), out var cachePath))
            {
                cachePath = entryCachePath / Guid.NewGuid().Encode();
                cachePairs[path.ToString()] = cachePath;
            }
            tasks.Add(Task.Run(async () =>
            {
                var cachePathValue = cachePath / "currentLatest";
                cachePath.CreateDirectory();
                if (path.FileExists())
                {
                    File.Move(path, cachePathValue, true);
                }
                else if (path.DirectoryExists())
                {
                    await path.MoveRecursively(cachePathValue);
                }
                Log.Information("{path} cache saved", path);
            }));
        }

        await Task.WhenAll(tasks);

        SetCacheIndex(cachePairs);
    }

    private void SetupTargetRunContext(ITargetEntryDefinition targetEntry, RunType runType, AppVersion? appVersion, string releaseNotes, PipelineRun pipeline)
    {
        if (appVersion == null || runType == RunType.Local)
        {
            targetEntry.RunContext = new LocalContext()
            {
                RunType = RunType.Local
            };
        }
        else if (runType == RunType.Bump)
        {
            targetEntry.RunContext = new BumpContext()
            {
                PipelineType = PipelineType,
                RunType = runType,
                AppVersion = new BumpReleaseVersion()
                {
                    AppId = appVersion.AppId,
                    Environment = appVersion.Environment,
                    Version = appVersion.Version,
                    BuildId = appVersion.BuildId,
                    ReleaseNotes = releaseNotes
                }
            };
        }
        else if (runType == RunType.PullRequest)
        {
            targetEntry.RunContext = new PullRequestContext()
            {
                PipelineType = PipelineType,
                RunType = runType,
                AppVersion = new PullRequestReleaseVersion()
                {
                    AppId = appVersion.AppId,
                    Environment = appVersion.Environment,
                    Version = appVersion.Version,
                    BuildId = appVersion.BuildId,
                    PullRequestNumber = pipeline.PipelineInfo.PullRequestNumber
                }
            };
        }
        else
        {
            targetEntry.RunContext = new VersionedContext()
            {
                PipelineType = PipelineType,
                RunType = runType,
                AppVersion = appVersion
            };
        }
    }

    private void SetupDependentRunContext(IDependentEntryDefinition dependentEntry, RunType runType)
    {
        if (runType == RunType.Local)
        {
            dependentEntry.RunContext = new LocalContext()
            {
                RunType = RunType.Local
            };
        }
        else
        {
            dependentEntry.RunContext = new CommitContext()
            {
                PipelineType = PipelineType,
                RunType = runType
            };
        }
    }

    private void EntryPreSetup(AllEntry allEntry, PipelineRun pipeline, PipelinePreSetup? pipelinePreSetup)
    {
        EntryHelpers.SetupSecretVariables(this);

        PipelineType = pipeline.PipelineType;

        foreach (var targetEntry in allEntry.TargetEntryDefinitionMap.Values)
        {
            if (pipelinePreSetup == null)
            {
                SetupTargetRunContext(targetEntry, RunType.Local, null, "", pipeline);
                continue;
            }

            if (!pipelinePreSetup.AppRunEntryMap.TryGetValue(ValueHelpers.GetOrNullFail(targetEntry.AppId), out var entry))
            {
                throw new Exception("App id not found");
            }
            if (!pipelinePreSetup.EntrySetupMap.TryGetValue(ValueHelpers.GetOrNullFail(targetEntry.Id), out var entrySetup))
            {
                throw new Exception("Entry id not found");
            }

            AppVersion appVersion = new()
            {
                AppId = entry.AppId.NotNullOrEmpty(),
                Environment = entry.Environment,
                Version = SemVersion.Parse(entry.Version, SemVersionStyles.Strict),
                BuildId = pipelinePreSetup.BuildId
            };

            SetupTargetRunContext(targetEntry, entrySetup.RunType, appVersion, pipelinePreSetup.ReleaseNotes, pipeline);
        }

        foreach (var dependentEntry in allEntry.DependentEntryDefinitionMap.Values)
        {
            if (pipelinePreSetup == null)
            {
                SetupDependentRunContext(dependentEntry, RunType.Local);
                continue;
            }

            if (!pipelinePreSetup.EntrySetupMap.TryGetValue(ValueHelpers.GetOrNullFail(dependentEntry.Id), out var entrySetup))
            {
                throw new Exception("Entry id not found");
            }

            SetupDependentRunContext(dependentEntry, entrySetup.RunType);
        }
    }

    private async Task RunEntry(AllEntry allEntry, PipelineRun pipeline, IEnumerable<IRunEntryDefinition> entriesToRun, PipelinePreSetup? pipelinePreSetup, bool skipCache, Func<IRunEntryDefinition, Task>? preExecute, Func<IRunEntryDefinition, Task>? postExecute)
    {
        await pipeline.Pipeline.PrepareEntryRun(allEntry, pipelinePreSetup, entriesToRun.ToDictionary(i => i.Id));

        CacheBump();

        EntryPreSetup(allEntry, pipeline, pipelinePreSetup);

        foreach (var entry in entriesToRun)
        {
            Console.WriteLine();
            Console.WriteLine($"""
                ═══════════════════════════════════════
                {entry.Id}
                ───────────────────────────────────────
                """);
            Console.WriteLine();

            OutputDirectory.CreateOrCleanDirectory();

            if (!skipCache)
            {
                await CachePreload(entry);
            }

            OutputBump();

            if (preExecute != null)
            {
                await preExecute.Invoke(entry);
            }

            await entry.GetExecute();

            if (postExecute != null)
            {
                await postExecute.Invoke(entry);
            }
            if (!skipCache)
            {
                await CachePostload(entry);
            }
        }

        await pipeline.Pipeline.FinalizeEntryRun(allEntry, pipelinePreSetup, entriesToRun.ToDictionary(i => i.Id));
    }

    private Task TestAppEntries(AllEntry allEntry, PipelineRun pipeline, IEnumerable<string> idsToRun, bool skipCache)
    {
        var pipelinePreSetup = pipeline.Pipeline.GetPipelinePreSetup();

        IEnumerable<IRunEntryDefinition> entriesToRun;

        if (!idsToRun.Any())
        {
            entriesToRun = allEntry.TestEntryDefinitionMap.Values.Cast<IRunEntryDefinition>();
        }
        else
        {
            entriesToRun = allEntry.TestEntryDefinitionMap.Values.Where(i => idsToRun.Any(j => j.Equals(i.Id)));
        }

        return RunEntry(allEntry, pipeline, entriesToRun, pipelinePreSetup, skipCache, null, null);
    }

    private Task BuildAppEntries(AllEntry allEntry, PipelineRun pipeline, IEnumerable<string> idsToRun, bool skipCache)
    {
        var pipelinePreSetup = pipeline.Pipeline.GetPipelinePreSetup();

        IEnumerable<IRunEntryDefinition> entriesToRun;

        if (!idsToRun.Any())
        {
            entriesToRun = allEntry.BuildEntryDefinitionMap.Values.Cast<IRunEntryDefinition>();
        }
        else
        {
            entriesToRun = allEntry.BuildEntryDefinitionMap.Values.Where(i => idsToRun.Any(j => j.Equals(i.Id)));
        }

        return RunEntry(allEntry, pipeline, entriesToRun, pipelinePreSetup, skipCache, null, async entry =>
        {
            IBuildEntryDefinition buildEntryDefinition = (entry as IBuildEntryDefinition)!;
            foreach (var asset in await buildEntryDefinition.GetReleaseAssets())
            {
                if (asset.FileExists())
                {
                    await asset.CopyRecursively(CommonOutputDirectory / "asset" / asset.Name);
                }
                else if (asset.DirectoryExists())
                {
                    var destinationPath = CommonOutputDirectory / "asset" / (asset.Name + ".zip");
                    if (destinationPath.FileExists())
                    {
                        destinationPath.DeleteFile();
                    }
                    asset.ZipTo(destinationPath);
                }
                Log.Information("Added {file} to release assets", asset);
            }
            foreach (var asset in await buildEntryDefinition.GetCommonReleaseAssets())
            {
                if (asset.FileExists() || asset.DirectoryExists())
                {
                    await asset.CopyRecursively(CommonOutputDirectory / "common_asset");
                    Log.Information("Added {file} to common assets", asset);
                }
            }

            var artifactName = buildEntryDefinition.AppId.NotNullOrEmpty().ToLowerInvariant() + artifactNameSeparator + buildEntryDefinition.Id.ToUpperInvariant();
            var artifactTempPath = TemporaryDirectory / artifactName;
            var artifactFilePath = CommonArtifactsDirectory / $"{artifactName}.zip";
            artifactTempPath.CreateOrCleanDirectory();
            artifactFilePath.DeleteFile();
            await CommonOutputDirectory.MoveRecursively(artifactTempPath);
            artifactTempPath.ZipTo(artifactFilePath);
        });
    }

    private Task PublishAppEntries(AllEntry allEntry, PipelineRun pipeline, IEnumerable<string> idsToRun, bool skipCache)
    {
        var pipelinePreSetup = pipeline.Pipeline.GetPipelinePreSetup();

        IEnumerable<IRunEntryDefinition> entriesToRun;

        if (!idsToRun.Any())
        {
            entriesToRun = allEntry.PublishEntryDefinitionMap.Values.Cast<IRunEntryDefinition>();
        }
        else
        {
            entriesToRun = allEntry.PublishEntryDefinitionMap.Values.Where(i => idsToRun.Any(j => j.Equals(i.Id)));
        }

        return RunEntry(allEntry, pipeline, entriesToRun, pipelinePreSetup, skipCache, entry =>
        {
            IPublishEntryDefinition publishEntryDefinition = (entry as IPublishEntryDefinition)!;
            if (CommonArtifactsDirectory.DirectoryExists())
            {
                foreach (var artifact in CommonArtifactsDirectory.GetFiles())
                {
                    if (!artifact.HasExtension(".zip"))
                    {
                        continue;
                    }
                    var appId = artifact.Name.Split(artifactNameSeparator).FirstOrDefault().NotNullOrEmpty().ToLowerInvariant();
                    if (appId.Equals(publishEntryDefinition.AppId, StringComparison.InvariantCultureIgnoreCase))
                    {
                        artifact.UnZipTo(CommonOutputDirectory);
                    }
                }
            }
            return Task.CompletedTask;
        }, null);
    }

    private void ValidateBumpVersion(AllVersions allVersions, string? bumpVersion)
    {
        if (!SemVersion.TryParse(bumpVersion, SemVersionStyles.Strict, out var inputVersion))
        {
            throw new Exception("Invalid semver version");
        }

        // Fail if current branch is not on the proper bump branch
        string env;
        if (inputVersion.IsPrerelease)
        {
            if (!Repository.Branch.Equals(inputVersion.PrereleaseIdentifiers[0], StringComparison.InvariantCultureIgnoreCase))
            {
                throw new Exception($"{inputVersion} should bump on {inputVersion.PrereleaseIdentifiers[0]} branch");
            }
            env = inputVersion.PrereleaseIdentifiers[0];
        }
        else
        {
            if (!Repository.Branch.Equals(MainEnvironmentBranch, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new Exception($"{inputVersion} should bump on {MainEnvironmentBranch.ToLowerInvariant()} branch");
            }
            env = MainEnvironmentBranch.ToLowerInvariant();
        }

        if (allVersions.EnvVersionGrouped.TryGetValue(env, out List<SemVersion>? value))
        {
            var lastVersion = value.Last();
            // Fail if the version is already released
            if (SemVersion.ComparePrecedence(lastVersion, inputVersion) == 0)
            {
                throw new Exception($"The latest version in the {env} releases is already {inputVersion}");
            }
            // Fail if the version is behind the latest release
            if (SemVersion.ComparePrecedence(lastVersion, inputVersion) > 0)
            {
                throw new Exception($"{inputVersion} is behind the latest version {lastVersion} in the {env} releases");
            }
        }
    }

    private async Task<List<(AppEntry AppEntry, SemVersion BumpVersion)>> InteractiveRelease()
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

        var allEntry = await ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this));

        string currentEnvIdentifier = Repository.Branch.ToLowerInvariant();

        IReadOnlyCollection<Output>? lsRemote = null;

        List<(AppEntry? AppEntry, AllVersions? AllVersions)> appEntryVersions = [];

        foreach (var pair in allEntry.AppEntryMap)
        {
            string appId = pair.Key;

            ValueHelpers.GetOrFail(() => EntryHelpers.GetAllVersions(this, appId, ref lsRemote), out var allVersions);

            appEntryVersions.Add((pair.Value, allVersions));
        }

        List<string> appEntryIdHasBump = [];
        foreach (var appEntryVersion in appEntryVersions)
        {
            if (appEntryVersion.AppEntry != null &&
                appEntryVersion.AllVersions != null &&
                appEntryVersion.AllVersions.EnvVersionGrouped.TryGetValue(currentEnvIdentifier, out var currentEnvVersions) &&
                currentEnvVersions.LastOrDefault() is SemVersion currentEnvLatestVersion &&
                appEntryVersion.AllVersions.VersionCommitPaired.TryGetValue(currentEnvLatestVersion, out var currentEnvLatestVersionCommitId) &&
                currentEnvLatestVersionCommitId == Repository.Commit)
            {
                appEntryIdHasBump.Add(appEntryVersion.AppEntry.AppId);
                Console.Write("Commit has already bumped ");
                ConsoleHelpers.WriteWithColor(appEntryVersion.AppEntry.AppId, ConsoleColor.DarkMagenta);
                Console.WriteLine();
            }
        }

        List<(AppEntry AppEntry, SemVersion BumpVersion)> appEntryVersionsToBump = [];

        appEntryVersions.Add((null, null));

        while (true)
        {
            var availableBump = appEntryVersions
                .Where(i =>
                {
                    if (appEntryVersionsToBump.Any(j => j.AppEntry.AppId.Equals(i.AppEntry?.AppId, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        return false;
                    }
                    if (appEntryIdHasBump.Any(j => j.Equals(i.AppEntry?.AppId, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        return false;
                    }

                    return true;
                });
            var appEntryVersion = Prompt.Select("App id to bump", availableBump, textSelector: (appEntry) => appEntry.AppEntry == null ? "->done" : appEntry.AppEntry.AppId);

            if (appEntryVersion.AppEntry == null || appEntryVersion.AllVersions == null)
            {
                if (appEntryVersionsToBump.Count != 0)
                {
                    var answer = Prompt.Confirm("Are you sure to bump selected version(s)?", defaultValue: false);
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

            appEntryVersion.AllVersions.EnvVersionGrouped.TryGetValue(currentEnvIdentifier, out var currentEnvLatestVersion);
            Console.Write("  Current latest version: ");
            ConsoleHelpers.WriteWithColor(currentEnvLatestVersion?.LastOrDefault()?.ToString() ?? "null", ConsoleColor.Green);
            Console.WriteLine("");
            List<Func<object, ValidationResult?>> validators =
            [
                Validators.Required(),
                (input => {
                    try
                    {
                        ValidateBumpVersion(appEntryVersion.AllVersions, input.ToString());
                    }
                    catch (Exception ex)
                    {
                        return new ValidationResult(ex.Message);
                    }

                    return ValidationResult.Success;
                })
            ];

            var bumpVersionStr = await Task.Run(() => Prompt.Input<string>("New Version", validators: validators));
            var bumpVersion = SemVersion.Parse(bumpVersionStr, SemVersionStyles.Strict);
            appEntryVersionsToBump.Add((appEntryVersion.AppEntry, bumpVersion));
        }

        return appEntryVersionsToBump;
    }

    private async Task RunBump(AllEntry allEntry, Dictionary<string, SemVersion> bumpMap)
    {
        if (bumpMap.Count == 0)
        {
            Log.Information("No version selected to bump.");
        }

        List<string> tagsToPush = [];

        foreach (var bumpPair in bumpMap)
        {
            if (!allEntry.AppEntryMap.ContainsKey(bumpPair.Key))
            {
                throw new Exception("No app entry for " + bumpPair.Key);
            }

            tagsToPush.Add(bumpPair.Key.ToLowerInvariant() + "/" + bumpPair.Value.ToString() + "-bump");
        }

        foreach (var tag in tagsToPush)
        {
            Git.Invoke($"tag {tag}", logInvocation: false, logOutput: false);
        }

        // ---------- Apply bump ----------

        await Task.Run(() =>
        {
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
        });
    }

    private async Task StartStatusWatch(bool cancelOnDone = false, params (string AppId, string Environment)[] appIds)
    {
        var allEntry = await ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this));

        ConsoleTableHeader[] headers =
            [
                ("App EntryId", HorizontalAlignment.Right),
                ("Environment", HorizontalAlignment.Center),
                ("Version", HorizontalAlignment.Right),
                ("Status", HorizontalAlignment.Center)
            ];

        CancellationTokenSource cts = new();
        Console.CancelKeyPress += delegate {
            cts.Cancel();
        };

        int lines = 0;

        while (!cts.IsCancellationRequested)
        {
            List<ConsoleTableRow> rows = [];

            IReadOnlyCollection<Output>? lsRemote = null;

            bool allDone = true;
            bool pullFailed = false;

            List<(string AppId, string Environment)> appIdsPassed = [];
            List<(string AppId, string Environment)> appIdsFailed = [];

            foreach (var appEntry in allEntry.AppEntryMap.Values)
            {
                AllVersions allVersions;
                try
                {
                    ValueHelpers.GetOrFail(() => EntryHelpers.GetAllVersions(this, appEntry.AppId, ref lsRemote), out allVersions);
                }
                catch
                {
                    pullFailed = true;
                    allDone = false;
                    break;
                }

                bool firstEntryRow = true;

                ConsoleColor statusColor = ConsoleColor.DarkGray;

                if (allVersions.EnvSorted.Count != 0)
                {
                    foreach (var env in allVersions.EnvSorted)
                    {
                        var bumpedVersion = allVersions.EnvVersionGrouped[env].Last();
                        string published;
                        if (allVersions.VersionFailed.Contains(bumpedVersion))
                        {
                            published = "Run Failed";
                            statusColor = ConsoleColor.Red;
                            appIdsFailed.Add((appEntry.AppId.ToLowerInvariant(), env.ToLowerInvariant()));
                        }
                        else if (allVersions.VersionPassed.Contains(bumpedVersion))
                        {
                            published = "Published";
                            statusColor = ConsoleColor.Green;
                            appIdsPassed.Add((appEntry.AppId.ToLowerInvariant(), env.ToLowerInvariant()));
                        }
                        else if (allVersions.VersionQueue.Contains(bumpedVersion))
                        {
                            published = "Publishing";
                            statusColor = ConsoleColor.DarkYellow;
                            allDone = false;
                        }
                        else if (allVersions.VersionBump.Contains(bumpedVersion))
                        {
                            published = "Waiting for queue";
                            statusColor = ConsoleColor.DarkYellow;
                            allDone = false;
                        }
                        else
                        {
                            published = "Not published";
                            statusColor = ConsoleColor.DarkGray;
                            allDone = false;
                        }
                        var bumpedVersionStr = SemverHelpers.IsVersionEmpty(bumpedVersion) ? "-" : bumpedVersion.ToString();
                        rows.Add(ConsoleTableRow.FromValue(
                            [
                                (firstEntryRow ? appEntry.AppId : "", ConsoleColor.Magenta),
                                (env, ConsoleColor.Magenta),
                                (bumpedVersionStr, ConsoleColor.Magenta),
                                (published, statusColor)
                            ]));
                        firstEntryRow = false;
                    }
                }
                else
                {
                    rows.Add(ConsoleTableRow.FromValue(
                        [
                            (appEntry.AppId, ConsoleColor.Magenta),
                            (null, ConsoleColor.Magenta),
                            (null, ConsoleColor.Magenta),
                            ("Not published", statusColor)
                        ]));
                }
                rows.Add(ConsoleTableRow.Separator);
            }
            if (rows.Count != 0)
            {
                rows.RemoveAt(rows.Count - 1);
            }

            Console.SetCursorPosition(0, int.Max(Console.CursorTop - lines, 0));

            if (pullFailed)
            {
                ConsoleHelpers.ClearCurrentConsoleLine();
                Console.Write("Time: " + DateTime.Now);
                Console.Write(", ");
                ConsoleHelpers.WriteWithColor("Error: Connection problems", ConsoleColor.Red);
                Console.WriteLine();
                lines = 0;
            }
            else
            {
                ConsoleHelpers.WriteLineClean("Time: " + DateTime.Now);
                lines = ConsoleTableHelpers.LogInfoTableWatch(headers, [.. rows]);
            }
            lines += 1;

            if (cancelOnDone)
            {
                if (allDone && appIds.Length == 0)
                {
                    break;
                }
                if (appIds.Any(appIdsFailed.Contains))
                {
                    Assert.Fail("Pipeline run has failed.");
                    break;
                }
                if (appIds.All(appIdsPassed.Contains))
                {
                    break;
                }
            }

            await Task.Delay(1000, cts.Token);
        }
    }
}
