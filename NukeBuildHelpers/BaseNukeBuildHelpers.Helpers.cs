using Nuke.Common;
using Nuke.Common.IO;
using System.Text.Json;
using NuGet.Packaging;
using System.Text.Json.Nodes;
using NukeBuildHelpers.Models;
using Semver;
using Serilog;
using YamlDotNet.Core.Tokens;
using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Common;
using System.Runtime.CompilerServices;
using System.Reflection;
using Microsoft.Extensions.DependencyModel;
using Nuke.Common.Tooling;
using Octokit;
using Microsoft.Identity.Client;
using NukeBuildHelpers.Attributes;
using System.Collections.Generic;
using Nuke.Common.Utilities;
using System.Net.Sockets;
using YamlDotNet.Serialization;
using NukeBuildHelpers.Interfaces;
using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.CI.GitHubActions;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Linq;
using NukeBuildHelpers.Models.RunContext;

namespace NukeBuildHelpers;

partial class BaseNukeBuildHelpers
{
    private void BuildWorkflow<T>()
        where T : IPipeline
    {
        (Activator.CreateInstance(typeof(T), this) as IPipeline)!.BuildWorkflow();
    }

    internal void SetupWorkflowBuilder(List<WorkflowBuilder> workflowBuilders, PipelineType pipelineType)
    {
        foreach (var workflowBuilder in workflowBuilders)
        {
            workflowBuilder.PipelineType = pipelineType;
            workflowBuilder.NukeBuild = this;
        }
    }

    private void SetupWorkflowRun(List<WorkflowStep> workflowSteps, AppConfig appConfig, PreSetupOutput? preSetupOutput)
    {
        var appEntrySecretMap = GetEntrySecretMap<AppEntry>();
        var appTestEntrySecretMap = GetEntrySecretMap<AppTestEntry>();

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
                foreach (var secret in testSecretMap.SecretHelpers)
                {
                    var envVarName = string.IsNullOrEmpty(secret.SecretHelper.EnvironmentVariableName) ? "NUKE_" + secret.SecretHelper.SecretVariableName : secret.SecretHelper.EnvironmentVariableName;
                    var secretValue = Environment.GetEnvironmentVariable(envVarName);
                    secret.MemberInfo.SetValue(appTestEntry, secretValue);
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
                foreach (var secret in appSecretMap.SecretHelpers)
                {
                    var envVarName = string.IsNullOrEmpty(secret.SecretHelper.EnvironmentVariableName) ? "NUKE_" + secret.SecretHelper.SecretVariableName : secret.SecretHelper.EnvironmentVariableName;
                    var secretValue = Environment.GetEnvironmentVariable(envVarName);
                    secret.MemberInfo.SetValue(appEntry.Value, secretValue);
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
        List<Task> tasks = [];
        List<Action> nonParallels = [];
        List<string> testAdded = [];

        List<WorkflowStep> workflowSteps = [.. GetInstances<WorkflowStep>().OrderByDescending(i => i.Priority)];

        SetupWorkflowRun(workflowSteps, appConfig, preSetupOutput);

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
                if (appEntryTest.RunParallel)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        foreach (var workflowStep in workflowSteps)
                        {
                            workflowStep.TestRun(appEntryTest);
                        }
                        appEntryTest.Run(appEntryTest.AppTestContext!);
                    }));
                }
                else
                {
                    nonParallels.Add(() =>
                    {
                        foreach (var workflowStep in workflowSteps)
                        {
                            workflowStep.TestRun(appEntryTest);
                        }
                        appEntryTest.Run(appEntryTest.AppTestContext!);
                    });
                }
            }
        }

        tasks.Add(Task.Run(async () =>
        {
            foreach (var nonParallel in nonParallels)
            {
                await Task.Run(nonParallel);
            }
        }));

        return Task.WhenAll(tasks);
    }

    private Task BuildAppEntries(AppConfig appConfig, IEnumerable<string> idsToRun, PreSetupOutput? preSetupOutput)
    {
        List<Task> tasks = [];
        List<Action> nonParallels = [];

        List<WorkflowStep> workflowSteps = [.. GetInstances<WorkflowStep>().OrderByDescending(i => i.Priority)];

        SetupWorkflowRun(workflowSteps, appConfig, preSetupOutput);

        if (preSetupOutput != null)
        {
            Directory.CreateDirectory(OutputDirectory);
            File.WriteAllText(OutputDirectory / "notes.md", preSetupOutput.ReleaseNotes);
        }

        foreach (var appEntry in appConfig.AppEntries)
        {
            if (idsToRun.Any() && !idsToRun.Any(i => i == appEntry.Key))
            {
                continue;
            }
            if (appEntry.Value.RunParallel)
            {
                tasks.Add(Task.Run(() =>
                {
                    foreach (var workflowStep in workflowSteps)
                    {
                        workflowStep.AppBuild(appEntry.Value);
                    }
                    appEntry.Value.Build(appEntry.Value.AppRunContext!);
                }));
            }
            else
            {
                nonParallels.Add(() =>
                {
                    foreach (var workflowStep in workflowSteps)
                    {
                        workflowStep.AppBuild(appEntry.Value);
                    }
                    appEntry.Value.Build(appEntry.Value.AppRunContext!);
                });
            }
        }

        tasks.Add(Task.Run(async () =>
        {
            foreach (var nonParallel in nonParallels)
            {
                await Task.Run(nonParallel);
            }
        }));

        return Task.WhenAll(tasks);
    }

    private Task PublishAppEntries(AppConfig appConfig, IEnumerable<string> idsToRun, PreSetupOutput? preSetupOutput)
    {
        List<Task> tasks = [];
        List<Action> nonParallels = [];

        List<WorkflowStep> workflowSteps = [.. GetInstances<WorkflowStep>().OrderByDescending(i => i.Priority)];

        SetupWorkflowRun(workflowSteps, appConfig, preSetupOutput);

        foreach (var appEntry in appConfig.AppEntries)
        {
            if (idsToRun.Any() && !idsToRun.Any(i => i == appEntry.Key))
            {
                continue;
            }
            if (appEntry.Value.RunParallel)
            {
                tasks.Add(Task.Run(() =>
                {
                    foreach (var workflowStep in workflowSteps)
                    {
                        workflowStep.AppPublish(appEntry.Value);
                    }
                    appEntry.Value.Publish(appEntry.Value.AppRunContext!);
                }));
            }
            else
            {
                nonParallels.Add(() =>
                {
                    foreach (var workflowStep in workflowSteps)
                    {
                        workflowStep.AppPublish(appEntry.Value);
                    }
                    appEntry.Value.Publish(appEntry.Value.AppRunContext!);
                });
            }
        }

        tasks.Add(Task.Run(async () =>
        {
            foreach (var nonParallel in nonParallels)
            {
                await Task.Run(nonParallel);
            }
        }));

        return Task.WhenAll(tasks);
    }

    internal static List<T> GetInstances<T>()
    {
        var asmNames = DependencyContext.Default!.GetDefaultAssemblyNames();

        var allTypes = asmNames.Select(Assembly.Load)
            .SelectMany(t => t.GetTypes())
            .Where(p => p.GetTypeInfo().IsSubclassOf(typeof(T)) && !p.IsAbstract);

        List<T> instances = [];
        foreach (Type type in allTypes)
        {
            instances.Add((T)Activator.CreateInstance(type)!);
        }
        return instances;
    }

    internal static AppConfig GetAppConfig()
    {
        Dictionary<string, AppEntryConfig> appEntryConfigs = [];

        bool hasMainReleaseEntry = false;
        List<AppEntry> appEntries = [];
        foreach (var appEntry in GetInstances<AppEntry>())
        {
            if (!appEntry.Enable)
            {
                continue;
            }
            if (appEntry.MainRelease)
            {
                if (hasMainReleaseEntry)
                {
                    throw new Exception("Contains multiple main release app entry");
                }
                hasMainReleaseEntry = true;
            }
            appEntries.Add(appEntry);
        }

        List<(bool IsAdded, AppTestEntry AppTestEntry)> appTestEntries = [];
        foreach (var appTestEntry in GetInstances<AppTestEntry>())
        {
            if (!appTestEntry.Enable)
            {
                continue;
            }
            if (appTestEntry.AppEntryTargets == null || appTestEntry.AppEntryTargets.Length == 0)
            {
                throw new Exception($"App test entry contains null or empty app entry id \"{appTestEntry.Name}\"");
            }
            appTestEntries.Add((false, appTestEntry));
        }

        foreach (var appEntry in appEntries)
        {
            if (appEntryConfigs.ContainsKey(appEntry.Id))
            {
                throw new Exception($"Contains multiple app entry id \"{appEntry.Id}\"");
            }
            List<AppTestEntry> appTestEntriesFound = [];
            for (int i = 0; appTestEntries.Count > i; i++)
            {
                if (appTestEntries[i].AppTestEntry.AppEntryTargets.Any(i => i == appEntry.GetType()))
                {
                    appTestEntriesFound.Add(appTestEntries[i].AppTestEntry);
                    appTestEntries[i] = (true, appTestEntries[i].AppTestEntry);
                }
            }
            appEntryConfigs.Add(appEntry.Id, new() { Entry = appEntry, Tests = appTestEntriesFound });
        }

        var nonAdded = appTestEntries.Where(i => !i.IsAdded);

        if (nonAdded.Any())
        {
            foreach (var (IsAdded, AppTestEntry) in nonAdded)
            {
                foreach (var appEntryTarget in AppTestEntry.AppEntryTargets)
                {
                    if (!appEntries.Any(i => string.Equals(i.Id, appEntryTarget.Name, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        throw new Exception($"App entry id \"{appEntryTarget.Name}\" does not exist, from app test entry \"{AppTestEntry.Name}\"");
                    }
                }
            }
        }

        return new()
        {
            AppEntryConfigs = appEntryConfigs,
            AppEntries = appEntries.ToDictionary(i => i.Id),
            AppTestEntries = appTestEntries.ToDictionary(i => i.AppTestEntry.Id, i => i.AppTestEntry)
        };
    }

    internal static Dictionary<string, (Type EntryType, List<(MemberInfo MemberInfo, SecretHelperAttribute SecretHelper)> SecretHelpers)> GetEntrySecretMap<T>()
        where T : Entry
    {
        var asmNames = DependencyContext.Default!.GetDefaultAssemblyNames();

        var allTypes = asmNames.Select(Assembly.Load)
            .SelectMany(t => t.GetTypes())
            .Where(p => p.GetTypeInfo().IsSubclassOf(typeof(T)) && !p.ContainsGenericParameters);

        Dictionary<string, (Type EntryType, List<(MemberInfo MemberInfo, SecretHelperAttribute SecretHelper)> SecretHelpers)> entry = [];
        foreach (Type type in allTypes)
        {
            foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                foreach (object attr in prop.GetCustomAttributes(true))
                {
                    if (attr is SecretHelperAttribute secretHelperAttr)
                    {
                        var id = ((T)Activator.CreateInstance(type)!).Id;
                        if (!entry.TryGetValue(id, out var secrets))
                        {
                            secrets = (type, []);
                            entry.Add(id, secrets);
                        }
                        secrets.SecretHelpers.Add((prop, secretHelperAttr));
                    }
                }
            }
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                foreach (object attr in field.GetCustomAttributes(true))
                {
                    if (attr is SecretHelperAttribute secretHelperAttr)
                    {
                        var id = ((T)Activator.CreateInstance(type)!).Id;
                        if (!entry.TryGetValue(id, out var secrets))
                        {
                            secrets = (type, []);
                            entry.Add(id, secrets);
                        }
                        secrets.SecretHelpers.Add((field, secretHelperAttr));
                    }
                }
            }
        }
        return entry;
    }

    private AllVersions GetAllVersions(string appId, Dictionary<string, AppEntryConfig> appEntryConfigs, ref IReadOnlyCollection<Output>? lsRemoteOutput)
    {
        GetOrFail(appId, appEntryConfigs, out _, out var appEntry);

        string basePeel = "refs/tags/";
        lsRemoteOutput ??= Git.Invoke("ls-remote -t -q", logOutput: false, logInvocation: false);

        Dictionary<string, List<long>> commitBuildIdGrouped = [];
        Dictionary<string, List<SemVersion>> commitVersionGrouped = [];
        Dictionary<string, List<string>> commitLatestTagGrouped = [];
        Dictionary<long, string> buildIdCommitPaired = [];
        Dictionary<SemVersion, string> versionCommitPaired = [];
        Dictionary<string, List<long>> envBuildIdGrouped = [];
        List<SemVersion> versionBump = [];
        List<SemVersion> versionQueue = [];
        List<SemVersion> versionFailed = [];
        List<SemVersion> versionPassed = [];
        foreach (var refs in lsRemoteOutput)
        {
            string rawTag = refs.Text[(refs.Text.IndexOf(basePeel) + basePeel.Length)..];
            string tag;
            string commitId = refs.Text[0..refs.Text.IndexOf(basePeel)].Trim();

            if (rawTag.StartsWith("build.", StringComparison.InvariantCultureIgnoreCase))
            {
                var buildSplit = rawTag.Split('-');
                var parsedBuildId = long.Parse(buildSplit[0].Split('.')[1]);
                if (buildSplit.Length == 1)
                {
                    if (!buildIdCommitPaired.ContainsKey(parsedBuildId))
                    {
                        buildIdCommitPaired[parsedBuildId] = commitId;
                    }
                    if (!commitBuildIdGrouped.TryGetValue(commitId, out var pairedCommitBuildId))
                    {
                        pairedCommitBuildId = [];
                        commitBuildIdGrouped.Add(commitId, pairedCommitBuildId);
                    }
                    if (!pairedCommitBuildId.Contains(parsedBuildId))
                    {
                        pairedCommitBuildId.Add(parsedBuildId);
                    }
                }
                else if (buildSplit.Length == 2)
                {
                    var buildIdEnv = buildSplit[1].ToLowerInvariant();
                    if (!EnvironmentBranches.Any(i => i.Equals(buildIdEnv, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        continue;
                    }
                    if (!commitBuildIdGrouped.TryGetValue(commitId, out var pairedCommitBuildId))
                    {
                        pairedCommitBuildId = [];
                        commitBuildIdGrouped.Add(commitId, pairedCommitBuildId);
                    }
                    if (!envBuildIdGrouped.TryGetValue(buildIdEnv, out var pairedEnvBuildId))
                    {
                        pairedEnvBuildId = [];
                        envBuildIdGrouped.Add(buildIdEnv, pairedEnvBuildId);
                    }
                    if (!buildIdCommitPaired.ContainsKey(parsedBuildId))
                    {
                        buildIdCommitPaired[parsedBuildId] = commitId;
                    }
                    if (!pairedCommitBuildId.Contains(parsedBuildId))
                    {
                        pairedCommitBuildId.Add(parsedBuildId);
                    }
                    if (!pairedEnvBuildId.Contains(parsedBuildId))
                    {
                        pairedEnvBuildId.Add(parsedBuildId);
                    }
                }
            }
            else
            {
                if (appEntry.Entry.MainRelease)
                {
                    tag = rawTag;
                }
                else if (rawTag.StartsWith(appId, StringComparison.InvariantCultureIgnoreCase))
                {
                    tag = rawTag[(rawTag.IndexOf(appId, StringComparison.InvariantCultureIgnoreCase) + appId.Length + 1)..];
                }
                else
                {
                    continue;
                }
                if (tag.StartsWith("latest", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (!commitLatestTagGrouped.TryGetValue(commitId, out var pairedLatestTag))
                    {
                        pairedLatestTag = [];
                        commitLatestTagGrouped.Add(commitId, pairedLatestTag);
                    }
                    pairedLatestTag.Add(tag);
                }
                else
                {
                    string versionTag = tag;
                    bool isBumpVersion = false;
                    bool isQueueVersion = false;
                    bool isFailedVersion = false;
                    bool isPassedVersion = false;
                    if (tag.EndsWith("-bump", StringComparison.InvariantCultureIgnoreCase))
                    {
                        versionTag = tag[..(tag.LastIndexOf("-bump"))];
                        isBumpVersion = true;
                    }
                    if (tag.EndsWith("-queue", StringComparison.InvariantCultureIgnoreCase))
                    {
                        versionTag = tag[..(tag.LastIndexOf("-queue"))];
                        isQueueVersion = true;
                    }
                    if (tag.EndsWith("-failed", StringComparison.InvariantCultureIgnoreCase))
                    {
                        versionTag = tag[..(tag.LastIndexOf("-failed"))];
                        isFailedVersion = true;
                    }
                    if (tag.EndsWith("-passed", StringComparison.InvariantCultureIgnoreCase))
                    {
                        versionTag = tag[..(tag.LastIndexOf("-passed"))];
                        isPassedVersion = true;
                    }

                    if (SemVersion.TryParse(versionTag, SemVersionStyles.Strict, out SemVersion tagSemver))
                    {
                        if (!commitVersionGrouped.TryGetValue(commitId, out var pairedVersion))
                        {
                            pairedVersion = [];
                            commitVersionGrouped.Add(commitId, pairedVersion);
                        }
                        tagSemver = tagSemver.WithoutMetadata();
                        versionCommitPaired[tagSemver] = commitId;
                        if (!pairedVersion.Contains(tagSemver))
                        {
                            pairedVersion.Add(tagSemver);
                        }
                        if (isBumpVersion)
                        {
                            versionBump.Add(tagSemver);
                        }
                        if (isQueueVersion)
                        {
                            versionQueue.Add(tagSemver);
                        }
                        if (isFailedVersion)
                        {
                            versionFailed.Add(tagSemver);
                        }
                        if (isPassedVersion)
                        {
                            versionPassed.Add(tagSemver);
                        }
                    }
                }
            }
        }

        List<SemVersion> allVersionList = commitVersionGrouped.SelectMany(i => i.Value).ToList();
        List<long> allBuildIdList = commitBuildIdGrouped.SelectMany(i => i.Value).ToList();
        Dictionary<string, List<SemVersion>> envVersionGrouped = allVersionList
            .GroupBy(i => i.IsPrerelease ? i.PrereleaseIdentifiers[0].Value.ToLowerInvariant() : "")
            .ToDictionary(i => i.Key, i => i.Select(j => j).ToList());

        Dictionary<string, (long BuildId, SemVersion Version)> pairedLatests = [];
        foreach (var pairedLatestTag in commitLatestTagGrouped)
        {
            string commitId = pairedLatestTag.Key;
            foreach (var latestTag in pairedLatestTag.Value)
            {
                if (!commitVersionGrouped.TryGetValue(commitId, out var versions) || versions.Count == 0)
                {
                    continue;
                }
                string env;
                var latestTagSplit = latestTag.Split('-');
                if (latestTagSplit.Length == 2)
                {
                    env = latestTagSplit[1];
                    if (!envBuildIdGrouped.TryGetValue(env, out var envBuildIds) || envBuildIds.Count == 0)
                    {
                        continue;
                    }
                    var latestVersion = versions.Where(i => i.IsPrerelease && i.PrereleaseIdentifiers[0].ToString().Equals(env, StringComparison.InvariantCultureIgnoreCase)).LastOrDefault();
                    if (latestVersion != null)
                    {
                        pairedLatests.Add(env, (envBuildIds.Max(), latestVersion));
                    }
                }
                else
                {
                    env = "main";
                    if (!envBuildIdGrouped.TryGetValue(env, out var envBuildIds) || envBuildIds.Count == 0)
                    {
                        continue;
                    }
                    var latestVersion = versions.Where(i => !i.IsPrerelease).LastOrDefault();
                    if (latestVersion != null)
                    {
                        pairedLatests.Add("", (envBuildIds.Max(), latestVersion));
                    }
                }
            }
        }

        Dictionary<string, SemVersion> envLatestVersionPaired = pairedLatests.ToDictionary(i => i.Key, i => i.Value.Version);
        Dictionary<string, long> envLatestBuildIdPaired = pairedLatests.ToDictionary(i => i.Key, i => i.Value.BuildId);
        List<string> envSorted = envVersionGrouped.Select(i => i.Key).ToList();

        envSorted.Sort();
        if (envSorted.Count > 0 && envSorted.First() == "")
        {
            var toMove = envSorted.First();
            envSorted.Remove(toMove);
            envSorted.Add(toMove);
        }
        foreach (var groupKey in envSorted)
        {
            var allVersion = envVersionGrouped[groupKey];
            allVersion.Sort(SemVersion.PrecedenceComparer);
        }

        return new()
        {
            CommitBuildIdGrouped = commitBuildIdGrouped,
            CommitLatestTagGrouped = commitLatestTagGrouped,
            CommitVersionGrouped = commitVersionGrouped,
            BuildIdCommitPaired = buildIdCommitPaired,
            VersionCommitPaired = versionCommitPaired,
            EnvVersionGrouped = envVersionGrouped,
            EnvBuildIdGrouped = envBuildIdGrouped,
            EnvLatestVersionPaired = envLatestVersionPaired,
            EnvLatestBuildIdPaired = envLatestBuildIdPaired,
            EnvSorted = envSorted,
            VersionBump = versionBump,
            VersionQueue = versionQueue,
            VersionFailed = versionFailed,
            VersionPassed = versionPassed,
        };
    }

    internal static void GetOrFail<T>(Func<T> valFactory, out T valOut)
    {
        try
        {
            valOut = valFactory();
        }
        catch (Exception ex)
        {
            Assert.Fail(ex.Message, ex);
            throw;
        }
    }

    internal static void GetOrFail(string appId, Dictionary<string, AppEntryConfig> appEntryConfigs, out string appIdOut, out AppEntryConfig appEntryConfig)
    {
        try
        {
            // Fail if appId is null and solution has multiple app entries
            if (string.IsNullOrEmpty(appId) && !appEntryConfigs.Any(ae => ae.Value.Entry.MainRelease))
            {
                throw new InvalidOperationException($"App entries has no main release, appId should not be empty");
            }

            // Fail if appId does not exists in app entries
            if (!string.IsNullOrEmpty(appId))
            {
                if (!appEntryConfigs.TryGetValue(appId.ToLowerInvariant(), out var aec) || aec == null)
                {
                    throw new InvalidOperationException($"App id \"{appId}\" does not exists");
                }
                appEntryConfig = aec;
            }
            else
            {
                appEntryConfig = appEntryConfigs.Where(ae => ae.Value.Entry.MainRelease).First().Value;
            }

            appIdOut = appEntryConfig.Entry.Id;
        }
        catch (Exception ex)
        {
            Assert.Fail(ex.Message, ex);
            throw;
        }
    }

    private static void LogInfoTable(IEnumerable<(string Text, HorizontalAlignment Alignment)> headers, params IEnumerable<string?>[] rows)
    {
        List<(int Length, string Text, HorizontalAlignment Alignment)> columns = [];

        foreach (var (Text, AlignRight) in headers)
        {
            columns.Add((Text.Length, Text, AlignRight));
        }

        foreach (var row in rows)
        {
            int rowCount = row.Count();
            for (int i = 0; i < rowCount; i++)
            {
                var rowElement = row.ElementAt(i);
                int rowWidth = rowElement?.Length ?? 0;
                columns[i] = (MathExtensions.Max(rowCount, columns[i].Length, rowWidth), columns[i].Text, columns[i].Alignment);
            }
        }

        string headerSeparator = "╬";
        string rowSeparator = "║";
        string textHeader = "║";
        foreach (var (Length, Text, AlignRight) in columns)
        {
            headerSeparator += new string('═', Length + 2) + '╬';
            rowSeparator += new string('-', Length + 2) + '║';
            textHeader += Text.PadCenter(Length + 2) + '║';
        }

        Console.WriteLine(headerSeparator);
        Console.WriteLine(textHeader);
        Console.WriteLine(headerSeparator);
        foreach (var row in rows)
        {
            var cells = row.Select(i => i?.ToString() ?? "null")?.ToArray() ?? [];
            if (row.All(i => i == "-"))
            {
                Console.WriteLine(rowSeparator);
            }
            else
            {
                int rowCount = row.Count();
                Console.Write("║ ");
                for (int i = 0; i < rowCount; i++)
                {
                    string rowText = cells[i];
                    var textRow = columns[i].Alignment switch
                    {
                        HorizontalAlignment.Left => rowText.PadLeft(columns[i].Length, rowText.Length),
                        HorizontalAlignment.Center => rowText.PadCenter(columns[i].Length, rowText.Length),
                        HorizontalAlignment.Right => rowText.PadRight(columns[i].Length, rowText.Length),
                        _ => throw new NotImplementedException()
                    };
                    ConsoleHelpers.WriteWithColor(textRow, ConsoleColor.Magenta);
                    Console.Write(" ║ ");
                }
                Console.WriteLine();
            }
        }
        Console.WriteLine(headerSeparator);
    }

    private static int LogInfoTableWatch(IEnumerable<(string Text, HorizontalAlignment Alignment)> headers, IEnumerable<(string? Text, ConsoleColor TextColor)>[] rows)
    {
        int lines = 0;

        List<(int Length, string Text, HorizontalAlignment Alignment)> columns = [];

        foreach (var (Text, AlignRight) in headers)
        {
            columns.Add((Text.Length, Text, AlignRight));
        }

        foreach (var row in rows)
        {
            int rowCount = row.Count();
            for (int i = 0; i < rowCount; i++)
            {
                var (Text, TextColor) = row.ElementAt(i);
                int rowWidth = Text?.Length ?? 0;
                columns[i] = (MathExtensions.Max(rowCount, columns[i].Length, rowWidth), columns[i].Text, columns[i].Alignment);
            }
        }

        string headerSeparator = "╬";
        string rowSeparator = "║";
        string textHeader = "║";
        foreach (var (Length, Text, AlignRight) in columns)
        {
            headerSeparator += new string('═', Length + 2) + '╬';
            rowSeparator += new string('-', Length + 2) + '║';
            textHeader += Text.PadCenter(Length + 2) + '║';
        }

        ConsoleHelpers.WriteLineClean(headerSeparator);
        ConsoleHelpers.WriteLineClean(textHeader);
        ConsoleHelpers.WriteLineClean(headerSeparator);
        lines++;
        lines++;
        lines++;
        foreach (var row in rows)
        {
            if (row.All(i => i.Text == "-"))
            {
                ConsoleHelpers.WriteLineClean(rowSeparator);
                lines++;
            }
            else
            {
                ConsoleHelpers.ClearCurrentConsoleLine();
                var cells = row.Select(i => i.Text?.ToString() ?? "null")?.ToArray() ?? [];
                int rowCount = row.Count();
                Console.Write("║ ");
                for (int i = 0; i < rowCount; i++)
                {
                    var (rowText, rowTextColor) = row.ElementAt(i);
                    int rowWidth = rowText == null ? 4 : rowText.Length;
                    var cellText = columns[i].Alignment switch
                    {
                        HorizontalAlignment.Left => cells[i].PadLeft(columns[i].Length, rowWidth),
                        HorizontalAlignment.Center => cells[i].PadCenter(columns[i].Length, rowWidth),
                        HorizontalAlignment.Right => cells[i].PadRight(columns[i].Length, rowWidth),
                        _ => throw new NotImplementedException()
                    };
                    ConsoleHelpers.WriteWithColor(cellText, rowTextColor);
                    Console.Write(" ║ ");
                }
                Console.WriteLine();
                lines++;
            }
        }
        ConsoleHelpers.WriteLineClean(headerSeparator);
        lines++;

        return lines;
    }

    public async Task StartStatusWatch(bool cancelOnDone = false, params (string AppId, string Environment)[] appIds)
    {
        GetOrFail(GetAppConfig, out var appConfig);

        List<(string Text, HorizontalAlignment Alignment)> headers =
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
            List<List<(string? Text, ConsoleColor TextColor)>> rows = [];

            IReadOnlyCollection<Output>? lsRemote = null;

            bool allDone = true;
            bool pullFailed = false;

            List<(string AppId, string Environment)> appIdsPassed = [];
            List<(string AppId, string Environment)> appIdsFailed = [];

            foreach (var key in appConfig.AppEntryConfigs.Select(i => i.Key))
            {
                string appId = key;

                GetOrFail(appId, appConfig.AppEntryConfigs, out appId, out var appEntry);
                AllVersions allVersions;
                try
                {
                    GetOrFail(() => GetAllVersions(appId, appConfig.AppEntryConfigs, ref lsRemote), out allVersions);
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
                        rows.Add(
                            [
                                (firstEntryRow ? appId : "", ConsoleColor.Magenta),
                                (env, ConsoleColor.Magenta),
                                (bumpedVersion?.ToString(), ConsoleColor.Magenta),
                                (published, statusColor)
                            ]);
                        firstEntryRow = false;
                    }
                }
                else
                {
                    rows.Add(
                        [
                            (appId, ConsoleColor.Magenta),
                            (null, ConsoleColor.Magenta),
                            (null, ConsoleColor.Magenta),
                            ("Not published", statusColor)
                        ]);
                }
                rows.Add(
                    [
                        ("-", ConsoleColor.Magenta),
                        ("-", ConsoleColor.Magenta),
                        ("-", ConsoleColor.Magenta),
                        ("-", ConsoleColor.Magenta)
                    ]);
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
                lines = LogInfoTableWatch(headers, [.. rows]);
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
