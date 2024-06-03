using Nuke.Common;
using Nuke.Common.IO;
using NukeBuildHelpers.Models;
using Semver;
using Serilog;
using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Common;
using Nuke.Common.Tooling;
using Nuke.Common.Utilities;
using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.CI.GitHubActions;
using Sharprompt;
using NukeBuildHelpers.Models.RunContext;
using System.ComponentModel.DataAnnotations;
using NukeBuildHelpers.ConsoleInterface;
using NukeBuildHelpers.ConsoleInterface.Models;
using NukeBuildHelpers.Pipelines.Interfaces;
using System.Text.Json;
using System.IO;
using System.Collections.Generic;

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

    internal void SetupWorkflowBuilder(List<WorkflowBuilder> workflowBuilders, PipelineType pipelineType)
    {
        foreach (var workflowBuilder in workflowBuilders)
        {
            workflowBuilder.PipelineType = pipelineType;
            workflowBuilder.NukeBuild = this;
        }
    }

    private static void CacheBump()
    {
        if (!CacheDirectory.DirectoryExists())
        {
            CacheDirectory.CreateDirectory();
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

    private static async Task CachePreload(Entry entry)
    {
        if (!entryCachePath.DirectoryExists())
        {
            entryCachePath.CreateDirectory();
        }

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
            if (!entry.CachePaths.Any(i => i == pair.Key))
            {
                cachePairs.Remove(pair.Key);
                pair.Value.DeleteDirectory();
                Log.Information("{path} cache cleaned", pair.Key);
            }
        }

        foreach (var path in entry.CachePaths)
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

    private static async Task CachePostload(Entry entry)
    {
        Dictionary<string, AbsolutePath> cachePairs = GetCacheIndex();

        List<Task> tasks = [];

        foreach (var path in entry.CachePaths)
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

    private void SetupWorkflowRun(List<WorkflowStep> workflowSteps, AppConfig appConfig, PreSetupOutput? preSetupOutput)
    {
        var appEntrySecretMap = AppEntryHelpers.GetEntrySecretMap<AppEntry>();
        var appTestEntrySecretMap = AppEntryHelpers.GetEntrySecretMap<AppTestEntry>();

        PipelineType pipelineType;

        IPipeline pipeline;
        PipelineInfo pipelineInfo;

        if (Host is AzurePipelines)
        {
            pipelineType = PipelineType.Azure;
            pipeline = new AzurePipeline(this);
        }
        else if (Host is GitHubActions)
        {
            pipelineType = PipelineType.Github;
            pipeline = new GithubPipeline(this);
        }
        else
        {
            throw new NotImplementedException();
        }

        pipelineInfo = pipeline.GetPipelineInfo();

        foreach (var workflowStep in workflowSteps)
        {
            workflowStep.PipelineType = pipelineType;
            workflowStep.NukeBuild = this;
        }

        foreach (var appTestEntry in appConfig.AppTestEntries.Values)
        {
            if (appTestEntrySecretMap.TryGetValue(appTestEntry.Id, out var testSecretMap) &&
                testSecretMap.EntryType == appTestEntry.GetType())
            {
                foreach (var (MemberInfo, Secret) in testSecretMap.Secrets)
                {
                    var envVarName = string.IsNullOrEmpty(Secret.EnvironmentVariableName) ? "NUKE_" + Secret.SecretVariableName : Secret.EnvironmentVariableName;
                    var secretValue = Environment.GetEnvironmentVariable(envVarName);
                    MemberInfo.SetValue(appTestEntry, secretValue);
                }
            }
            appTestEntry.PipelineType = pipelineType;
            appTestEntry.NukeBuild = this;
            RunTestType runTestType = RunTestType.Local;
            foreach (var appEntry in appConfig.AppEntries.Values)
            {
                if (appTestEntry.AppEntryTargets.Any(i => i == appEntry.GetType()))
                {
                    runTestType = RunTestType.Target;
                    break;
                }
            }
            appTestEntry.AppTestContext = new()
            {
                OutputDirectory = BaseHelper.OutputDirectory,
                RunTestType = runTestType
            };
        }

        RunType runType = RunType.Local;
        if (preSetupOutput != null)
        {
            if (preSetupOutput.TriggerType == TriggerType.PullRequest)
            {
                runType = RunType.PullRequest;
            }
            else if (preSetupOutput.TriggerType == TriggerType.Commit)
            {
                runType = RunType.Commit;
            }
            else if (preSetupOutput.TriggerType == TriggerType.Tag)
            {
                if (preSetupOutput.HasRelease)
                {
                    runType = RunType.Bump;
                }
                else
                {
                    runType = RunType.Commit;
                }
            }
        }

        foreach (var appEntry in appConfig.AppEntries)
        {
            if (appEntrySecretMap.TryGetValue(appEntry.Value.Id, out var appSecretMap) &&
                appSecretMap.EntryType == appEntry.Value.GetType())
            {
                foreach (var (MemberInfo, Secret) in appSecretMap.Secrets)
                {
                    var envVarName = string.IsNullOrEmpty(Secret.EnvironmentVariableName) ? "NUKE_" + Secret.SecretVariableName : Secret.EnvironmentVariableName;
                    var secretValue = Environment.GetEnvironmentVariable(envVarName);
                    MemberInfo.SetValue(appEntry.Value, secretValue);
                }
            }

            appEntry.Value.PipelineType = pipelineType;
            appEntry.Value.NukeBuild = this;

            AppVersion? appVersion = null;

            if (preSetupOutput != null &&
                preSetupOutput.Entries.TryGetValue(appEntry.Value.Id, out var preSetupOutputVersion))
            {
                appVersion = new AppVersion()
                {
                    AppId = appEntry.Value.Id,
                    Environment = preSetupOutputVersion.Environment,
                    Version = SemVersion.Parse(preSetupOutputVersion.Version, SemVersionStyles.Strict),
                    BuildId = preSetupOutput.BuildId,
                    ReleaseNotes = preSetupOutput.ReleaseNotes
                };
            }

            if (appVersion == null)
            {
                appEntry.Value.AppRunContext = new AppLocalRunContext()
                {
                    OutputDirectory = BaseHelper.OutputDirectory,
                    RunType = runType,
                };
            }
            else if (runType == RunType.Bump)
            {
                appEntry.Value.AppRunContext = new AppBumpRunContext()
                {
                    OutputDirectory = BaseHelper.OutputDirectory,
                    RunType = runType,
                    AppVersion = appVersion
                };
            }
            else if (runType == RunType.PullRequest)
            {
                appEntry.Value.AppRunContext = new AppPullRequestRunContext()
                {
                    OutputDirectory = BaseHelper.OutputDirectory,
                    RunType = runType,
                    AppVersion = appVersion,
                    PullRequestNumber = pipelineInfo.PullRequestNumber
                };
            }
            else
            {
                appEntry.Value.AppRunContext = new AppCommitRunContext()
                {
                    OutputDirectory = BaseHelper.OutputDirectory,
                    RunType = runType,
                    AppVersion = appVersion
                };
            }
        }
    }

    private Task TestAppEntries(AppConfig appConfig, IEnumerable<string> idsToRun, PreSetupOutput? preSetupOutput)
    {
        List<Func<Task>> tasks = [];
        List<string> testAdded = [];

        List<WorkflowStep> workflowSteps = [.. ClassHelpers.GetInstances<WorkflowStep>().OrderByDescending(i => i.Priority)];

        SetupWorkflowRun(workflowSteps, appConfig, preSetupOutput);

        CacheBump();

        foreach (var appEntry in appConfig.AppEntries)
        {
            if (idsToRun.Any() && !idsToRun.Any(i => i == appEntry.Key))
            {
                continue;
            }
            var appEntryType = appEntry.Value.GetType();
            foreach (var appEntryTest in appConfig.AppTestEntries.Values.Where(i => i.AppEntryTargets.Any(j => j == appEntryType)))
            {
                if (idsToRun.Any() && !idsToRun.Any(i => i == appEntryTest.Id))
                {
                    continue;
                }
                if (testAdded.Contains(appEntryTest.Name))
                {
                    continue;
                }
                testAdded.Add(appEntryTest.Name);
                tasks.Add(() => Task.Run(async () =>
                {
                    await CachePreload(appEntryTest);
                    foreach (var workflowStep in workflowSteps)
                    {
                        workflowStep.TestRun(appEntryTest);
                    }
                    appEntryTest.Run(appEntryTest.AppTestContext!);
                    await appEntryTest.RunAsync(appEntryTest.AppTestContext!);
                    await CachePostload(appEntryTest);
                }));
            }
        }

        return Task.Run(async () =>
        {
            foreach (var task in tasks)
            {
                await task();
            }
        });
    }

    private Task BuildAppEntries(AppConfig appConfig, IEnumerable<string> idsToRun, PreSetupOutput? preSetupOutput)
    {
        List<Func<Task>> tasks = [];

        List<WorkflowStep> workflowSteps = [.. ClassHelpers.GetInstances<WorkflowStep>().OrderByDescending(i => i.Priority)];

        SetupWorkflowRun(workflowSteps, appConfig, preSetupOutput);

        OutputDirectory.DeleteDirectory();
        OutputDirectory.CreateDirectory();

        CacheBump();

        if (preSetupOutput != null)
        {
            (OutputDirectory / "notes.md").WriteAllText(preSetupOutput.ReleaseNotes);
        }

        foreach (var appEntry in appConfig.AppEntries)
        {
            if (idsToRun.Any() && !idsToRun.Any(i => i == appEntry.Key))
            {
                continue;
            }
            tasks.Add(() => Task.Run(async () =>
            {
                await CachePreload(appEntry.Value);
                foreach (var workflowStep in workflowSteps)
                {
                    workflowStep.AppBuild(appEntry.Value);
                    await workflowStep.AppBuildAsync(appEntry.Value);
                }
                appEntry.Value.Build(appEntry.Value.AppRunContext!);
                await appEntry.Value.BuildAsync(appEntry.Value.AppRunContext!);
                await CachePostload(appEntry.Value);
            }));
        }

        return Task.Run(async () =>
        {
            foreach (var task in tasks)
            {
                await task();
            }
        });
    }

    private Task PublishAppEntries(AppConfig appConfig, IEnumerable<string> idsToRun, PreSetupOutput? preSetupOutput)
    {
        List<Func<Task>> tasks = [];

        List<WorkflowStep> workflowSteps = [.. ClassHelpers.GetInstances<WorkflowStep>().OrderByDescending(i => i.Priority)];

        SetupWorkflowRun(workflowSteps, appConfig, preSetupOutput);

        CacheBump();

        foreach (var appEntry in appConfig.AppEntries)
        {
            if (idsToRun.Any() && !idsToRun.Any(i => i == appEntry.Key))
            {
                continue;
            }
            tasks.Add(() => Task.Run(async () =>
            {
                await CachePreload(appEntry.Value);
                foreach (var workflowStep in workflowSteps)
                {
                    workflowStep.AppPublish(appEntry.Value);
                    await workflowStep.AppPublishAsync(appEntry.Value);
                }
                appEntry.Value.Publish(appEntry.Value.AppRunContext!);
                await appEntry.Value.PublishAsync(appEntry.Value.AppRunContext!);
                await CachePostload(appEntry.Value);
            }));
        }

        return Task.Run(async () =>
        {
            foreach (var task in tasks)
            {
                await task();
            }
        });
    }

    private async Task<List<(AppEntry AppEntry, AllVersions AllVersions, SemVersion BumpVersion)>> InteractiveRelease()
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

        ValueHelpers.GetOrFail(AppEntryHelpers.GetAppConfig, out var appConfig);

        string currentEnvIdentifier = Repository.Branch.ToLowerInvariant();

        IReadOnlyCollection<Output>? lsRemote = null;

        List<(AppEntry? AppEntry, AllVersions? AllVersions)> appEntryVersions = [];

        foreach (var pair in appConfig.AppEntryConfigs)
        {
            string appId = pair.Key;

            ValueHelpers.GetOrFail(appId, appConfig.AppEntryConfigs, out appId, out var appEntryConfig);
            ValueHelpers.GetOrFail(() => AppEntryHelpers.GetAllVersions(this, appId, appConfig.AppEntryConfigs, ref lsRemote), out var allVersions);

            appEntryVersions.Add((pair.Value.Entry, allVersions));
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
                appEntryIdHasBump.Add(appEntryVersion.AppEntry.Id);
                Console.Write("Commit has already bumped ");
                ConsoleHelpers.WriteWithColor(appEntryVersion.AppEntry.Id, ConsoleColor.DarkMagenta);
                Console.WriteLine();
            }
        }

        List<(AppEntry AppEntry, AllVersions AllVersions, SemVersion BumpVersion)> appEntryVersionsToBump = [];

        appEntryVersions.Add((null, null));

        while (true)
        {
            var availableBump = appEntryVersions
                .Where(i =>
                {
                    if (appEntryVersionsToBump.Any(j => j.AppEntry.Id == i.AppEntry?.Id))
                    {
                        return false;
                    }
                    if (appEntryIdHasBump.Any(j => j == i.AppEntry?.Id))
                    {
                        return false;
                    }

                    return true;
                });
            var appEntryVersion = Prompt.Select("App id to bump", availableBump, textSelector: (appEntry) => appEntry.AppEntry == null ? "->done" : appEntry.AppEntry.Id);

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
            appEntryVersionsToBump.Add((appEntryVersion.AppEntry, appEntryVersion.AllVersions, bumpVersion));
        }

        return appEntryVersionsToBump;
    }

    private async Task<List<(AppEntry AppEntry, AllVersions AllVersions, SemVersion BumpVersion)>> StartBump()
    {
        var appEntryVersionsToBump = await InteractiveRelease();

        if (appEntryVersionsToBump.Count == 0)
        {
            Log.Information("No version selected to bump.");
            return appEntryVersionsToBump;
        }

        List<string> tagsToPush = [];

        foreach (var appEntryVersionToBump in appEntryVersionsToBump)
        {
            if (appEntryVersionToBump.AppEntry.MainRelease)
            {
                tagsToPush.Add(appEntryVersionToBump.BumpVersion.ToString() + "-bump");
            }
            else
            {
                tagsToPush.Add(appEntryVersionToBump.AppEntry.Id.ToLowerInvariant() + "/" + appEntryVersionToBump.BumpVersion.ToString() + "-bump");
            }
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

        return appEntryVersionsToBump;
    }

    private async Task StartStatusWatch(bool cancelOnDone = false, params (string AppId, string Environment)[] appIds)
    {
        ValueHelpers.GetOrFail(AppEntryHelpers.GetAppConfig, out var appConfig);

        ConsoleTableHeader[] headers =
            [
                ("App Id", HorizontalAlignment.Right),
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

            foreach (var key in appConfig.AppEntryConfigs.Select(i => i.Key))
            {
                string appId = key;

                ValueHelpers.GetOrFail(appId, appConfig.AppEntryConfigs, out appId, out var appEntry);
                AllVersions allVersions;
                try
                {
                    ValueHelpers.GetOrFail(() => AppEntryHelpers.GetAllVersions(this, appId, appConfig.AppEntryConfigs, ref lsRemote), out allVersions);
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
                            appIdsFailed.Add((appId.ToLowerInvariant(), env.ToLowerInvariant()));
                        }
                        else if (allVersions.VersionPassed.Contains(bumpedVersion))
                        {
                            published = "Published";
                            statusColor = ConsoleColor.Green;
                            appIdsPassed.Add((appId.ToLowerInvariant(), env.ToLowerInvariant()));
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
                                (firstEntryRow ? appId : "", ConsoleColor.Magenta),
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
                            (appId, ConsoleColor.Magenta),
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
