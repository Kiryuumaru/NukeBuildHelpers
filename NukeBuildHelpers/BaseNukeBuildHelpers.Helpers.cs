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
using NukeBuildHelpers.Common.Models;
using NukeBuildHelpers.Entry.Helpers;
using System.Collections;

namespace NukeBuildHelpers;

partial class BaseNukeBuildHelpers
{
    private static readonly AbsolutePath entryCachePath = CommonCacheDirectory / "entry";
    private static readonly AbsolutePath entryCacheIndexPath = CommonCacheDirectory / "entry_index";

    internal void CheckEnvironementBranches()
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

    private static void CacheBump()
    {
        if (!CommonCacheOutputDirectory.DirectoryExists())
        {
            CommonCacheOutputDirectory.CreateDirectory();
        }

        (CommonCacheDirectory / "stamp").WriteAllText(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
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

    private static async Task CachePreload(IEntryDefinition entry)
    {
        if (!entryCachePath.DirectoryExists())
        {
            entryCachePath.CreateDirectory();
        }

        var cachePaths = entry.CachePaths == null ? [] : await entry.GetCachePaths();

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
            tasks.Add(Task.Run(() =>
            {
                var cachePathValue = cachePath / "value";
                path.Parent.CreateDirectory();
                if (cachePathValue.FileExists())
                {
                    File.Move(cachePathValue, path, true);
                }
                else if (cachePathValue.DirectoryExists())
                {
                    cachePathValue.MoveFilesRecursively(path);
                }
                Log.Information("{path} cache loaded", path);
            }));
        }

        await Task.WhenAll(tasks);

        SetCacheIndex(cachePairs);
    }

    private static async Task CachePostload(IEntryDefinition entry)
    {
        var cachePaths = entry.CachePaths == null ? [] : await entry.GetCachePaths();

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
            tasks.Add(Task.Run(() =>
            {
                var cachePathValue = cachePath / "value";
                cachePath.CreateDirectory();
                if (path.FileExists())
                {
                    File.Move(path, cachePathValue, true);
                }
                else if (path.DirectoryExists())
                {
                    path.MoveFilesRecursively(cachePathValue);
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

    private async Task StartPreSetup(AllEntry allEntry)
    {
        var pipeline = PipelineHelpers.SetupPipeline(this);

        await pipeline.Pipeline.PreparePreSetup(allEntry);

        Log.Information("Target branch: {branch}", pipeline.PipelineInfo.Branch);
        Log.Information("Trigger type: {branch}", pipeline.PipelineInfo.TriggerType);

        EntryHelpers.SetupSecretVariables(this);

        PipelineType = pipeline.PipelineType;

        string env = pipeline.PipelineInfo.Branch.ToLowerInvariant();

        IReadOnlyCollection<Output>? lsRemote = null;

        Dictionary<string, AppRunEntry> toEntry = [];

        long targetBuildId = 0;
        long lastBuildId = 0;

        foreach (var key in allEntry.AppEntryMap.Select(i => i.Key))
        {
            string appId = key;

            ValueHelpers.GetOrFail(appId, allEntry, out var appEntry);
            ValueHelpers.GetOrFail(() => EntryHelpers.GetAllVersions(this, appId, ref lsRemote), out var allVersions);

            if (allVersions.BuildIdCommitPaired.Count > 0)
            {
                var maxBuildId = allVersions.BuildIdCommitPaired.Select(i => i.Key).Max();
                lastBuildId = Math.Max(maxBuildId, lastBuildId);
            }

            if (allVersions.EnvSorted.Count == 0 || !allVersions.EnvVersionGrouped.TryGetValue(env, out var versionGroup) || versionGroup.Count == 0)
            {
                continue;
            }

            var lastVersionGroup = versionGroup.Last();

            bool hasBumped = false;

            if (!allVersions.EnvLatestVersionPaired.TryGetValue(env, out var value) || value != lastVersionGroup)
            {
                if (allVersions.VersionBump.Contains(lastVersionGroup) &&
                    !allVersions.VersionQueue.Contains(lastVersionGroup) &&
                    !allVersions.VersionFailed.Contains(lastVersionGroup) &&
                    !allVersions.VersionPassed.Contains(lastVersionGroup))
                {
                    allVersions.EnvLatestBuildIdPaired.TryGetValue(env, out var allVersionLastId);
                    targetBuildId = targetBuildId == 0 ? allVersionLastId : Math.Min(allVersionLastId, targetBuildId);
                    if (pipeline.PipelineInfo.TriggerType == TriggerType.Tag)
                    {
                        hasBumped = true;
                        Log.Information("{appId} Tag: {current}, current latest: {latest}", appId, value?.ToString(), lastVersionGroup.ToString());
                    }
                }
            }
            else
            {
                if (pipeline.PipelineInfo.TriggerType == TriggerType.Tag)
                {
                    Log.Information("{appId} Tag: {current}, already latest", appId, lastVersionGroup.ToString());
                }
            }

            toEntry.Add(appId, new()
            {
                AppId = appEntry.AppId,
                Environment = env,
                Version = lastVersionGroup.ToString(),
                HasRelease = hasBumped
            });
        }

        foreach (var rel in toEntry.Values.Where(i => i.HasRelease))
        {
            Log.Information("{appId} on {env} has new version {newVersion}", rel.AppId, rel.Environment, rel.Version);
        }

        var releaseNotes = "";
        var buildId = lastBuildId + 1;
        var buildTag = "build." + buildId;
        var targetBuildTag = "build." + targetBuildId;
        var isFirstRelease = targetBuildId == 0;
        var hasEntries = toEntry.Count != 0;
        var hasRelease = toEntry.Any(i => i.Value.HasRelease);

        string versionFactory(string version)
        {
            var semVersion = SemVersion.Parse(version, SemVersionStyles.Strict);
            if (pipeline.PipelineInfo.TriggerType == TriggerType.PullRequest)
            {
                return SemVersion.Parse($"{semVersion.WithoutMetadata()}+build.{buildId}-pr.{pipeline.PipelineInfo.PullRequestNumber}", SemVersionStyles.Strict).ToString();
            }
            else
            {
                return SemVersion.Parse($"{semVersion.WithoutMetadata()}+build.{buildId}", SemVersionStyles.Strict).ToString();
            }
        }

        foreach (var entry in toEntry.Values)
        {
            toEntry[entry.AppId] = new()
            {
                AppId = entry.AppId,
                Environment = entry.Environment,
                Version = versionFactory(entry.Version),
                HasRelease = entry.HasRelease
            };
        }

        if (hasRelease && pipeline.PipelineType != PipelineType.Local)
        {
            foreach (var entry in toEntry.Values.Where(i => i.HasRelease))
            {
                var semVersion = SemVersion.Parse(entry.Version, SemVersionStyles.Strict).WithoutMetadata();
                Git.Invoke("tag -f " + entry.AppId.ToLowerInvariant() + "/" + semVersion + "-queue");
                Git.Invoke("tag -f " + entry.AppId.ToLowerInvariant() + "/" + semVersion);
            }

            Git.Invoke("tag -f " + buildTag, logger: (s, e) => Log.Debug(e));
            Git.Invoke("tag -f " + buildTag + "-" + env, logger: (s, e) => Log.Debug(e));

            Git.Invoke("push -f --tags", logger: (s, e) => Log.Debug(e));

            string ghReleaseCreateArgs = $"release create {buildTag} " +
                $"--title {buildTag} " +
                $"--target {pipeline.PipelineInfo.Branch} " +
                $"--generate-notes " +
                $"--draft";

            if (!isFirstRelease)
            {
                ghReleaseCreateArgs += " --notes-start-tag " + targetBuildTag;
            }

            Gh.Invoke(ghReleaseCreateArgs, logger: (s, e) => Log.Debug(e));

            var releaseNotesJson = Gh.Invoke($"release view {buildTag} --json body", logger: (s, e) => Log.Debug(e)).FirstOrDefault().Text;
            var releaseNotesJsonDocument = JsonSerializer.Deserialize<JsonDocument>(releaseNotesJson);
            if (releaseNotesJsonDocument == null ||
                !releaseNotesJsonDocument.RootElement.TryGetProperty("body", out var releaseNotesProp) ||
                releaseNotesProp.GetString() is not string releaseNotesFromProp)
            {
                throw new Exception("releaseNotesJsonDocument is empty");
            }
            releaseNotes = releaseNotesFromProp;
        }

        foreach (var targetEntry in allEntry.TargetEntryDefinitionMap.Values)
        {
            if (!toEntry.TryGetValue(ValueHelpers.GetOrNullFail(targetEntry.AppId), out var entry))
            {
                continue;
            }

            RunType runType = pipeline.PipelineInfo.TriggerType switch
            {
                TriggerType.PullRequest => RunType.PullRequest,
                TriggerType.Commit => RunType.Commit,
                TriggerType.Tag => entry.HasRelease ? RunType.Bump : RunType.Commit,
                TriggerType.Local => RunType.Local,
                _ => throw new NotSupportedException()
            };

            AppVersion appVersion = new()
            {
                AppId = entry.AppId.NotNullOrEmpty(),
                Environment = entry.Environment,
                Version = SemVersion.Parse(entry.Version, SemVersionStyles.Strict),
                BuildId = buildId
            };

            SetupTargetRunContext(targetEntry, runType, appVersion, releaseNotes, pipeline);
        }

        Dictionary<string, EntrySetup> testEntrySetupMap = [];
        Dictionary<string, EntrySetup> buildEntrySetupMap = [];
        Dictionary<string, EntrySetup> publishEntrySetupMap = [];
        Dictionary<string, EntrySetup> targetEntrySetupMap = [];
        Dictionary<string, EntrySetup> dependentEntrySetupMap = [];
        Dictionary<string, EntrySetup> entrySetupMap = [];

        async Task<EntrySetup> createEntrySetup(IEntryDefinition entry)
        {
            var runnerOs = await entry.GetRunnerOS();
            var cachePaths = await entry.GetCachePaths();
            var flatCachePaths = cachePaths.Select(i =>
            {
                string flatPath = i.ToString();
                if (!flatPath.StartsWith(RootDirectory))
                {
                    throw new Exception("Invalid cache path");
                }
                return flatPath[RootDirectory.ToString().Length..];
            });

            EntrySetup setup = new()
            {
                Id = entry.Id,
                Condition = await entry.GetCondition(),
                CacheInvalidator = await entry.GetCacheInvalidator(),
                CachePaths = flatCachePaths.ToArray(),
                RunType = ValueHelpers.GetOrNullFail(entry.RunContext).RunType,
                RunnerOSSetup = new()
                {
                    Name = runnerOs.Name,
                    RunnerPipelineOS = JsonSerializer.Serialize(runnerOs.GetPipelineOS(PipelineType), JsonExtension.SnakeCaseNamingOptionIndented),
                    RunScript = runnerOs.GetRunScript(PipelineType)
                }
            };

            return setup;
        }

        foreach (var entry in allEntry.PublishEntryDefinitionMap.Values)
        {
            EntrySetup setup = await createEntrySetup(entry);
            publishEntrySetupMap.Add(entry.Id, setup);
            targetEntrySetupMap.Add(entry.Id, setup);
            entrySetupMap.Add(entry.Id, setup);
        }

        foreach (var entry in allEntry.BuildEntryDefinitionMap.Values)
        {
            EntrySetup setup = await createEntrySetup(entry);
            buildEntrySetupMap.Add(entry.Id, setup);
            targetEntrySetupMap.Add(entry.Id, setup);
            entrySetupMap.Add(entry.Id, setup);
        }

        foreach (var dependentEntry in allEntry.DependentEntryDefinitionMap.Values)
        {
            List<(EntrySetup Setup, IBuildEntryDefinition Entry)> buildEntries = [];
            List<(EntrySetup Setup, IPublishEntryDefinition Entry)> publishEntries = [];
            List<(EntrySetup Setup, ITargetEntryDefinition Entry)> targetEntries = [];

            foreach (var appId in dependentEntry.AppIds)
            {
                foreach (var entrySetup in entrySetupMap.Values)
                {
                    if (allEntry.TargetEntryDefinitionMap.TryGetValue(entrySetup.Id, out var targetEntry) &&
                        ValueHelpers.GetOrNullFail(targetEntry.AppId).Equals(appId, StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (targetEntry is IBuildEntryDefinition buildEntry)
                        {
                            buildEntries.Add((entrySetup, buildEntry));
                        }
                        else if (targetEntry is IPublishEntryDefinition publishEntry)
                        {
                            publishEntries.Add((entrySetup, publishEntry));
                        }
                    }
                }
            }

            RunType runType = pipeline.PipelineInfo.TriggerType switch
            {
                TriggerType.PullRequest => RunType.PullRequest,
                TriggerType.Commit => RunType.Commit,
                TriggerType.Tag => targetEntries.Any(i => i.Entry.RunContext is IBumpContext) ? RunType.Bump : RunType.Commit,
                TriggerType.Local => RunType.Local,
                _ => throw new NotSupportedException()
            };

            if (targetEntries.Any(i => i.Setup.Condition))
            {
                runType |= RunType.Target;
            }

            SetupDependentRunContext(dependentEntry, runType);
        }

        foreach (var entry in allEntry.DependentEntryDefinitionMap.Values)
        {
            EntrySetup setup = await createEntrySetup(entry);
            testEntrySetupMap.Add(entry.Id, setup);
            dependentEntrySetupMap.Add(entry.Id, setup);
            entrySetupMap.Add(entry.Id, setup);
        }

        PipelinePreSetup pipelinePreSetup = new()
        {
            Branch = pipeline.PipelineInfo.Branch,
            TriggerType = pipeline.PipelineInfo.TriggerType,
            Environment = env,
            ReleaseNotes = releaseNotes,
            BuildId = buildId,
            LastBuildId = targetBuildId,
            PullRequestNumber = pipeline.PipelineInfo.PullRequestNumber,
            HasRelease = hasRelease,
            TestEntries = [.. testEntrySetupMap.Keys],
            BuildEntries = [.. buildEntrySetupMap.Keys],
            PublishEntries = [.. publishEntrySetupMap.Keys],
            EntrySetupMap = entrySetupMap,
            AppRunEntryMap = toEntry
        };

        await pipeline.Pipeline.FinalizePreSetup(allEntry, pipelinePreSetup);
    }

    private void EntryPreSetup(AllEntry allEntry, PipelineRun pipeline, PipelinePreSetup pipelinePreSetup)
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

    private async Task RunEntry(AllEntry allEntry, PipelineRun pipeline, IEnumerable<IEntryDefinition> entriesToRun, PipelinePreSetup pipelinePreSetup)
    {
        List<Func<Task>> tasks = [];

        await pipeline.Pipeline.PrepareEntryRun(allEntry, pipelinePreSetup, entriesToRun.ToDictionary(i => i.Id));

        CacheBump();

        EntryPreSetup(allEntry, pipeline, pipelinePreSetup);

        foreach (var entry in entriesToRun)
        {
            tasks.Add(() => Task.Run(async () =>
            {
                await CachePreload(entry);
                await entry.GetExecute();
                await CachePostload(entry);
            }));
        }

        foreach (var task in tasks)
        {
            await task();
        }

        await pipeline.Pipeline.FinalizeEntryRun(allEntry, pipelinePreSetup, entriesToRun.ToDictionary(i => i.Id));
    }

    private Task TestAppEntries(AllEntry allEntry, IEnumerable<string> idsToRun)
    {
        var pipeline = PipelineHelpers.SetupPipeline(this);

        var pipelinePreSetup = pipeline.Pipeline.GetPipelinePreSetup();

        IEnumerable<IEntryDefinition> entriesToRun;

        if (!idsToRun.Any())
        {
            entriesToRun = allEntry.TestEntryDefinitionMap.Values.Cast<IEntryDefinition>();
        }
        else
        {
            entriesToRun = allEntry.TestEntryDefinitionMap.Values.Where(i => idsToRun.Any(j => j.Equals(i.Id)));
        }

        return RunEntry(allEntry, pipeline, entriesToRun, pipelinePreSetup);
    }

    private Task BuildAppEntries(AllEntry allEntry, IEnumerable<string> idsToRun)
    {
        var pipeline = PipelineHelpers.SetupPipeline(this);

        var pipelinePreSetup = pipeline.Pipeline.GetPipelinePreSetup();

        IEnumerable<IEntryDefinition> entriesToRun;

        OutputDirectory.DeleteDirectory();
        OutputDirectory.CreateDirectory();

        (OutputDirectory / "notes.md").WriteAllText(pipelinePreSetup.ReleaseNotes);

        if (!idsToRun.Any())
        {
            entriesToRun = allEntry.BuildEntryDefinitionMap.Values.Cast<IEntryDefinition>();
        }
        else
        {
            entriesToRun = allEntry.BuildEntryDefinitionMap.Values.Where(i => idsToRun.Any(j => j.Equals(i.Id)));
        }

        return RunEntry(allEntry, pipeline, entriesToRun, pipelinePreSetup);
    }

    private Task PublishAppEntries(AllEntry allEntry, IEnumerable<string> idsToRun)
    {
        var pipeline = PipelineHelpers.SetupPipeline(this);

        var pipelinePreSetup = pipeline.Pipeline.GetPipelinePreSetup();

        IEnumerable<IEntryDefinition> entriesToRun;

        if (!idsToRun.Any())
        {
            entriesToRun = allEntry.PublishEntryDefinitionMap.Values.Cast<IEntryDefinition>();
        }
        else
        {
            entriesToRun = allEntry.PublishEntryDefinitionMap.Values.Where(i => idsToRun.Any(j => j.Equals(i.Id)));
        }

        return RunEntry(allEntry, pipeline, entriesToRun, pipelinePreSetup);
    }

    private async Task StartPostSetup(AllEntry allEntry)
    {
        var pipeline = PipelineHelpers.SetupPipeline(this);

        var pipelinePreSetup = pipeline.Pipeline.GetPipelinePreSetup();

        await pipeline.Pipeline.PreparePostSetup(allEntry, pipelinePreSetup);

        bool success = true;

        foreach (var entryDefinition in allEntry.EntryDefinitionMap.Values)
        {
            var entryRunResult = Environment.GetEnvironmentVariable("NUKE_RUN_RESULT_" + entryDefinition.Id.ToUpperInvariant());
            Log.Information("{entryId} result: {result}", entryDefinition.Id, entryRunResult);
            if (entryRunResult == "error" && success)
            {
                success = false;
            }
        }

        if (success)
        {
            if (pipelinePreSetup.HasRelease)
            {
                foreach (var appRunEntry in pipelinePreSetup.AppRunEntryMap.Values.Where(i => i.HasRelease))
                {
                    if (!allEntry.AppEntryMap.TryGetValue(appRunEntry.AppId, out var appEntry))
                    {
                        continue;
                    }
                    var appIdLower = appEntry.AppId.ToLowerInvariant();
                    var releasePath = OutputDirectory / appIdLower;
                    if (!releasePath.DirectoryExists())
                    {
                        throw new Exception("No release found for " + appIdLower);
                    }
                    var outPath = OutputDirectory / appIdLower + "-" + appRunEntry.Version;
                    var outPathZip = OutputDirectory / appIdLower + "-" + appRunEntry.Version + ".zip";
                    releasePath.CopyFilesRecursively(outPath);
                    outPath.ZipTo(outPathZip);
                    Log.Information("Publish: {name}", appIdLower);
                }

                await Task.Run(() =>
                {
                    foreach (var appRunEntry in pipelinePreSetup.AppRunEntryMap.Values.Where(i => i.HasRelease))
                    {
                        if (!allEntry.AppEntryMap.TryGetValue(appRunEntry.AppId, out var appEntry))
                        {
                            continue;
                        }
                        var appIdLower = appEntry.AppId.ToLowerInvariant();
                        var version = SemVersion.Parse(appRunEntry.Version, SemVersionStyles.Strict).WithoutMetadata();
                        string latestTag = "latest";
                        if (!appRunEntry.Environment.Equals(MainEnvironmentBranch, StringComparison.InvariantCultureIgnoreCase))
                        {
                            latestTag += "-" + appRunEntry.Environment;
                        }

                        Git.Invoke("tag -f " + appIdLower + "/" + version + "-passed");
                        Git.Invoke("tag -f " + appIdLower + "/" + version);
                        Git.Invoke("tag -f " + appIdLower + "/" + latestTag);
                    }

                    Git.Invoke("push -f --tags", logger: (s, e) => Log.Debug(e));

                    Gh.Invoke("release upload --clobber build." + pipelinePreSetup.BuildId + " " + string.Join(" ", OutputDirectory.GetFiles("*.zip").Select(i => i.ToString())));

                    Gh.Invoke("release edit --draft=false build." + pipelinePreSetup.BuildId);
                });
            }
        }
        else
        {
            if (pipelinePreSetup.HasRelease)
            {
                await Task.Run(() =>
                {
                    foreach (var appRunEntry in pipelinePreSetup.AppRunEntryMap.Values.Where(i => i.HasRelease))
                    {
                        var version = SemVersion.Parse(appRunEntry.Version, SemVersionStyles.Strict).WithoutMetadata();
                        string latestTag = "latest";
                        if (!appRunEntry.Environment.Equals(MainEnvironmentBranch, StringComparison.InvariantCultureIgnoreCase))
                        {
                            latestTag += "-" + appRunEntry.Environment;
                        }

                        Git.Invoke("tag -f " + appRunEntry.AppId + "/" + version + "-failed");
                        Git.Invoke("tag -f " + appRunEntry.AppId + "/" + version);
                    }

                    Gh.Invoke("release delete -y build." + pipelinePreSetup.BuildId);

                    Git.Invoke("push -f --tags", logger: (s, e) => Log.Debug(e));
                });
            }
        }

        await pipeline.Pipeline.FinalizePostSetup(allEntry, pipelinePreSetup);

        if (!success)
        {
            throw new Exception("Run has error(s)");
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

        ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this), out var allEntry);

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
            List<Func<object, ValidationResult?>> validators = [Validators.Required(),
                    (input => {
                        if (!SemVersion.TryParse(input.ToString(), SemVersionStyles.Strict, out var inputVersion))
                        {
                            return new ValidationResult("Invalid semver version");
                        }
                        
                        // Fail if current branch is not on the proper bump branch
                        string env;
                        if (inputVersion.IsPrerelease)
                        {
                            if (!Repository.Branch.Equals(inputVersion.PrereleaseIdentifiers[0], StringComparison.InvariantCultureIgnoreCase))
                            {
                                return new ValidationResult($"{inputVersion} should bump on {inputVersion.PrereleaseIdentifiers[0]} branch");
                            }
                            env = inputVersion.PrereleaseIdentifiers[0];
                        }
                        else
                        {
                            if (!Repository.Branch.Equals(MainEnvironmentBranch, StringComparison.InvariantCultureIgnoreCase))
                            {
                                return new ValidationResult($"{inputVersion} should bump on {MainEnvironmentBranch.ToLowerInvariant()} branch");
                            }
                            env = MainEnvironmentBranch.ToLowerInvariant();
                        }

                        if (appEntryVersion.AllVersions.EnvVersionGrouped.TryGetValue(env, out List<SemVersion>? value))
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
                    })];

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

    private async Task<List<(AppEntry AppEntry, SemVersion BumpVersion)>> StartBump(AllEntry allEntry)
    {
        var appEntryVersionsToBump = await InteractiveRelease();

        await RunBump(allEntry, appEntryVersionsToBump.ToDictionary(i => i.AppEntry.AppId, i => i.BumpVersion));

        return appEntryVersionsToBump;
    }

    private async Task StartStatusWatch(bool cancelOnDone = false, params (string AppId, string Environment)[] appIds)
    {
        ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this), out var allEntry);

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
