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

    private void SetupWorkflowRun(List<WorkflowStep> workflowSteps, Dictionary<string, (AppEntry Entry, List<AppTestEntry> Tests)> appEntries, PreSetupOutput? preSetupOutput)
    {
        var appEntrySecretMap = GetEntrySecretMap<AppEntry>();
        var appTestEntrySecretMap = GetEntrySecretMap<AppTestEntry>();

        PipelineType pipelineType;

        if (Host is AzurePipelines)
        {
            pipelineType = PipelineType.Azure;
        }
        else if (Host is GitHubActions)
        {
            pipelineType = PipelineType.Github;
        }
        else
        {
            throw new NotImplementedException();
        }

        foreach (var workflowStep in workflowSteps)
        {
            workflowStep.PipelineType = pipelineType;
            workflowStep.NukeBuild = this;
        }

        foreach (var appEntry in appEntries)
        {
            if (appEntrySecretMap.TryGetValue(appEntry.Value.Entry.Id, out var appSecretMap) &&
                appSecretMap.EntryType == appEntry.Value.Entry.GetType())
            {
                foreach (var secret in appSecretMap.SecretHelpers)
                {
                    var envVarName = string.IsNullOrEmpty(secret.SecretHelper.EnvironmentVariableName) ? "NUKE_" + secret.SecretHelper.SecretVariableName : secret.SecretHelper.EnvironmentVariableName;
                    var secretValue = Environment.GetEnvironmentVariable(envVarName);
                    secret.MemberInfo.SetValue(appEntry.Value.Entry, secretValue);
                }
            }

            appEntry.Value.Entry.PipelineType = pipelineType;
            appEntry.Value.Entry.NukeBuild = this;

            foreach (var appTestEntry in appEntry.Value.Tests)
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
            }
            if (preSetupOutput != null && preSetupOutput.HasRelease)
            {
                foreach (var release in preSetupOutput.Releases)
                {
                    if (appEntry.Value.Entry.Id == release.Key)
                    {
                        appEntry.Value.Entry.NewVersion = new NewVersion()
                        {
                            Environment = release.Value.Environment,
                            Version = SemVersion.Parse(release.Value.Version, SemVersionStyles.Strict),
                            BuildId = preSetupOutput.BuildId,
                            ReleaseNotes = preSetupOutput.ReleaseNotes
                        };
                    }
                }
            }
        }
    }

    private Task TestAppEntries(Dictionary<string, (AppEntry Entry, List<AppTestEntry> Tests)> appEntries, IEnumerable<string> idsToRun, PreSetupOutput? preSetupOutput)
    {
        List<Task> tasks = [];
        List<Action> nonParallels = [];
        List<string> testAdded = [];

        List<WorkflowStep> workflowSteps = [.. GetInstances<WorkflowStep>().OrderByDescending(i => i.Priority)];

        SetupWorkflowRun(workflowSteps, appEntries, preSetupOutput);

        foreach (var appEntry in appEntries)
        {
            if (idsToRun.Any() && !idsToRun.Any(i => i == appEntry.Key))
            {
                continue;
            }
            foreach (var appEntryTest in appEntry.Value.Tests)
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
                        appEntryTest.Run();
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
                        appEntryTest.Run();
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

    private Task BuildAppEntries(Dictionary<string, (AppEntry Entry, List<AppTestEntry> Tests)> appEntries, IEnumerable<string> idsToRun, PreSetupOutput? preSetupOutput)
    {
        List<Task> tasks = [];
        List<Action> nonParallels = [];

        List<WorkflowStep> workflowSteps = [.. GetInstances<WorkflowStep>().OrderByDescending(i => i.Priority)];

        SetupWorkflowRun(workflowSteps, appEntries, preSetupOutput);

        if (preSetupOutput != null)
        {
            Directory.CreateDirectory(OutputDirectory);
            File.WriteAllText(OutputDirectory / "notes.md", preSetupOutput.ReleaseNotes);
        }

        foreach (var appEntry in appEntries)
        {
            if (idsToRun.Any() && !idsToRun.Any(i => i == appEntry.Key))
            {
                continue;
            }
            if (appEntry.Value.Entry.RunParallel)
            {
                tasks.Add(Task.Run(() =>
                {
                    foreach (var workflowStep in workflowSteps)
                    {
                        workflowStep.AppBuild(appEntry.Value.Entry);
                    }
                    appEntry.Value.Entry.Build();
                }));
            }
            else
            {
                nonParallels.Add(() =>
                {
                    foreach (var workflowStep in workflowSteps)
                    {
                        workflowStep.AppBuild(appEntry.Value.Entry);
                    }
                    appEntry.Value.Entry.Build();
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

    private Task PublishAppEntries(Dictionary<string, (AppEntry Entry, List<AppTestEntry> Tests)> appEntries, IEnumerable<string> idsToRun, PreSetupOutput? preSetupOutput)
    {
        List<Task> tasks = [];
        List<Action> nonParallels = [];

        List<WorkflowStep> workflowSteps = [.. GetInstances<WorkflowStep>().OrderByDescending(i => i.Priority)];

        SetupWorkflowRun(workflowSteps, appEntries, preSetupOutput);

        foreach (var appEntry in appEntries)
        {
            if (idsToRun.Any() && !idsToRun.Any(i => i == appEntry.Key))
            {
                continue;
            }
            if (appEntry.Value.Entry.RunParallel)
            {
                tasks.Add(Task.Run(() =>
                {
                    foreach (var workflowStep in workflowSteps)
                    {
                        workflowStep.AppPublish(appEntry.Value.Entry);
                    }
                    appEntry.Value.Entry.Publish();
                }));
            }
            else
            {
                nonParallels.Add(() =>
                {
                    foreach (var workflowStep in workflowSteps)
                    {
                        workflowStep.AppPublish(appEntry.Value.Entry);
                    }
                    appEntry.Value.Entry.Publish();
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

    internal static Dictionary<string, (AppEntry Entry, List<AppTestEntry> Tests)> GetAppEntryConfigs()
    {
        Dictionary<string, (AppEntry Entry, List<AppTestEntry> Tests)> configs = [];

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
            if (configs.ContainsKey(appEntry.Id))
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
            configs.Add(appEntry.Id, (appEntry, appTestEntriesFound));
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

        return configs;
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

    private AllVersions GetAllVersions(string appId, Dictionary<string, (AppEntry Entry, List<AppTestEntry> Tests)> appEntryConfigs, ref IReadOnlyCollection<Output>? lsRemoteOutput)
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
        List<long> buildIdPassed = [];
        List<long> buildIdFailed = [];
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
                else if (buildSplit.Length == 3)
                {
                    var buildIdRunIndicator = buildSplit[2].ToLowerInvariant();
                    if (buildIdRunIndicator.Equals("passed", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (!buildIdPassed.Contains(parsedBuildId))
                        {
                            buildIdPassed.Add(parsedBuildId);
                        }
                    }
                    else if (buildIdRunIndicator.Equals("failed", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (!buildIdFailed.Contains(parsedBuildId))
                        {
                            buildIdFailed.Add(parsedBuildId);
                        }
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
                else if (SemVersion.TryParse(tag, SemVersionStyles.Strict, out SemVersion tagSemver))
                {
                    if (!commitVersionGrouped.TryGetValue(commitId, out var pairedVersion))
                    {
                        pairedVersion = [];
                        commitVersionGrouped.Add(commitId, pairedVersion);
                    }
                    versionCommitPaired[tagSemver] = commitId;
                    pairedVersion.Add(tagSemver);
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
            BuildIdPassed = buildIdPassed,
            BuildIdFailed = buildIdFailed,
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

    internal static void GetOrFail(string appId, Dictionary<string, (AppEntry Entry, List<AppTestEntry> Tests)> appEntryConfigs, out string appIdOut, out (AppEntry Entry, List<AppTestEntry> Tests) appEntryConfig)
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
                if (!appEntryConfigs.TryGetValue(appId.ToLowerInvariant(), out appEntryConfig))
                {
                    throw new InvalidOperationException($"App id \"{appId}\" does not exists");
                }
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

    internal static void GetOrFail(string? rawValue, out SemVersion valOut)
    {
        try
        {
            if (!SemVersion.TryParse(rawValue, SemVersionStyles.Strict, out valOut))
            {
                throw new ArgumentException($"{rawValue} is not a valid semver version");
            }
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
        GetOrFail(GetAppEntryConfigs, out var appEntryConfigs);

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

            foreach (var key in appEntryConfigs.Select(i => i.Key))
            {
                string appId = key;

                GetOrFail(appId, appEntryConfigs, out appId, out var appEntry);
                AllVersions allVersions;
                try
                {
                    GetOrFail(() => GetAllVersions(appId, appEntryConfigs, ref lsRemote), out allVersions);
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
                        allVersions.EnvLatestVersionPaired.TryGetValue(groupKey, out var releasedVersion);
                        string published;
                        if (bumpedVersion != releasedVersion)
                        {
                            if (bumpedVersion != null)
                            {
                                var bumpedCommitId = allVersions.VersionCommitPaired[bumpedVersion];
                                if (releasedVersion != null)
                                {
                                    if (allVersions.EnvLatestBuildIdPaired.TryGetValue(groupKey, out var releasedBuildId) &&
                                        allVersions.BuildIdCommitPaired.TryGetValue(releasedBuildId, out var buildIdCommitId) &&
                                        bumpedCommitId == buildIdCommitId)
                                    {
                                        if (allVersions.BuildIdFailed.Contains(releasedBuildId))
                                        {
                                            published = "Run Failed1";
                                            statusColor = ConsoleColor.Red;
                                            appIdsFailed.Add((appId, env));
                                        }
                                        else
                                        {
                                            published = "Publishing2";
                                            statusColor = ConsoleColor.Yellow;
                                            allDone = false;
                                        }
                                    }
                                    else
                                    {
                                        published = "Waiting for queue3";
                                        statusColor = ConsoleColor.Yellow;
                                        allDone = false;
                                    }
                                }
                                else
                                {
                                    if (allVersions.EnvBuildIdGrouped.TryGetValue(groupKey, out var envBuildIdGroup) &&
                                        envBuildIdGroup.Count != 0 &&
                                        envBuildIdGroup.Max() is long envBuildIdGroupMax &&
                                        allVersions.BuildIdCommitPaired.TryGetValue(envBuildIdGroupMax, out var buildIdCommitId) &&
                                        bumpedCommitId == buildIdCommitId)
                                    {
                                        if (allVersions.BuildIdFailed.Contains(envBuildIdGroupMax))
                                        {
                                            published = "Run Failed4";
                                            statusColor = ConsoleColor.Red;
                                            appIdsFailed.Add((appId, env));
                                        }
                                        else
                                        {
                                            published = "Publishing5";
                                            statusColor = ConsoleColor.Yellow;
                                            allDone = false;
                                        }
                                    }
                                    else
                                    {
                                        published = "Waiting for queue6";
                                        statusColor = ConsoleColor.Yellow;
                                        allDone = false;
                                    }
                                }
                            }
                            else
                            {
                                published = "Not published";
                                statusColor = ConsoleColor.DarkGray;
                                allDone = false;
                            }
                        }
                        else
                        {
                            published = "Published";
                            statusColor = ConsoleColor.Green;
                            appIdsPassed.Add((appId, env));
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
                if (appIds.Any(i => appIdsFailed.Contains(i)))
                {
                    Assert.Fail("Pipeline run has failed.");
                    break;
                }
                if (appIds.All(i => appIdsPassed.Contains(i)))
                {
                    break;
                }
            }

            await Task.Delay(1000, cts.Token);
        }
    }
}
