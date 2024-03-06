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

namespace NukeBuildHelpers;

partial class BaseNukeBuildHelpers
{
    private void SetupAppEntries(Dictionary<string, (AppEntry Entry, List<AppTestEntry> Tests)> appEntries, PreSetupOutput? preSetupOutput)
    {
        var appEntrySecretMap = GetEntrySecretMap<AppEntry>();
        var appTestEntrySecretMap = GetEntrySecretMap<AppTestEntry>();

        foreach (var appEntry in appEntries)
        {
            if (appEntrySecretMap.TryGetValue(appEntry.Value.Entry.Id, out var appSecretMap) &&
                appSecretMap.EntryType == appEntry.Value.Entry.GetType())
            {
                foreach (var secret in appSecretMap.SecretHelpers)
                {
                    var secretValue = Environment.GetEnvironmentVariable(secret.SecretHelper.Name);
                    secret.MemberInfo.SetValue(appEntry.Value.Entry, secretValue);
                }
            }

            appEntry.Value.Entry.NukeBuild = this;
            appEntry.Value.Entry.OutputPath = OutputPath;
            foreach (var appTestEntry in appEntry.Value.Tests)
            {
                if (appTestEntrySecretMap.TryGetValue(appTestEntry.Id, out var testSecretMap) &&
                    testSecretMap.EntryType == appTestEntry.GetType())
                {
                    foreach (var secret in testSecretMap.SecretHelpers)
                    {
                        var secretValue = Environment.GetEnvironmentVariable(secret.SecretHelper.Name);
                        secret.MemberInfo.SetValue(appTestEntry, secretValue);
                    }
                }

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

        SetupAppEntries(appEntries, preSetupOutput);

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
                if (appEntry.Value.Entry.RunParallel)
                {
                    tasks.Add(Task.Run(() => appEntryTest.Run()));
                }
                else
                {
                    nonParallels.Add(() => appEntryTest.Run());
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

        SetupAppEntries(appEntries, preSetupOutput);

        foreach (var appEntry in appEntries)
        {
            if (idsToRun.Any() && !idsToRun.Any(i => i == appEntry.Key))
            {
                continue;
            }
            if (appEntry.Value.Entry.RunParallel)
            {
                tasks.Add(Task.Run(() => appEntry.Value.Entry.Build()));
            }
            else
            {
                nonParallels.Add(() => appEntry.Value.Entry.Build());
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

        SetupAppEntries(appEntries, preSetupOutput);

        foreach (var appEntry in appEntries)
        {
            if (idsToRun.Any() && !idsToRun.Any(i => i == appEntry.Key))
            {
                continue;
            }
            if (appEntry.Value.Entry.RunParallel)
            {
                tasks.Add(Task.Run(() => appEntry.Value.Entry.Publish()));
            }
            else
            {
                nonParallels.Add(() => appEntry.Value.Entry.Publish());
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

    internal static List<T> GetEntries<T>()
        where T : BaseEntry
    {
        var asmNames = DependencyContext.Default!.GetDefaultAssemblyNames();

        var allTypes = asmNames.Select(Assembly.Load)
            .SelectMany(t => t.GetTypes())
            .Where(p => p.GetTypeInfo().IsSubclassOf(typeof(T)) && !p.ContainsGenericParameters);

        List<T> entry = [];
        foreach (Type type in allTypes)
        {
            entry.Add((T)Activator.CreateInstance(type)!);
        }
        return entry;
    }

    internal static Dictionary<string, (AppEntry Entry, List<AppTestEntry> Tests)> GetAppEntryConfigs()
    {
        Dictionary<string, (AppEntry Entry, List<AppTestEntry> Tests)> configs = [];

        bool hasMainReleaseEntry = false;
        List<AppEntry> appEntries = [];
        foreach (var appEntry in GetEntries<AppEntry>())
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
        foreach (var appTestEntry in GetEntries<AppTestEntry>())
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
        where T : BaseEntry
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

        Dictionary<string, (string Env, List<long> BuildIds, List<SemVersion> Versions, List<string> LatestTags)> pairedTags = [];
        foreach (var refs in lsRemoteOutput)
        {
            string rawTag = refs.Text[(refs.Text.IndexOf(basePeel) + basePeel.Length)..];
            string tag;
            string commitId = refs.Text[0..refs.Text.IndexOf(basePeel)].Trim();

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
            if (!pairedTags.TryGetValue(commitId, out var vals))
            {
                vals = (null!, [], [], []);
                pairedTags.Add(commitId, vals);
            }
            if (tag.StartsWith("latest", StringComparison.InvariantCultureIgnoreCase))
            {
                vals.LatestTags.Add(tag);
            }
            if (tag.StartsWith("build.", StringComparison.InvariantCultureIgnoreCase))
            {
                vals.BuildIds.Add(long.Parse(tag.Replace("build.", "")));
            }
            if (SemVersion.TryParse(tag, SemVersionStyles.Strict, out SemVersion tagSemver))
            {
                vals.Versions.Add(tagSemver);
                string env = tagSemver.IsPrerelease ? tagSemver.PrereleaseIdentifiers[0].Value.ToLowerInvariant() : "";
                pairedTags[commitId] = (env, vals.BuildIds, vals.Versions, vals.LatestTags);
            }
        }
        pairedTags = pairedTags.Where(i => i.Value.Env != null).ToDictionary();

        Dictionary<string, (List<long> BuildIds, List<SemVersion> Versions, List<string> LatestTags)> pairedEnvGroup =
            pairedTags.Values.GroupBy(i => i.Env).ToDictionary(
                i => i.Key,
                i => (
                    i.SelectMany(j => j.BuildIds).ToList(),
                    i.SelectMany(j => j.Versions).ToList(),
                    i.SelectMany(j => j.LatestTags).ToList()));
        Dictionary<string, (long BuildId, SemVersion Version) > pairedLatests = pairedTags
            .Where(i =>
            {
                string latestIndicator = i.Value.Env == "" ? "latest" : "latest-" + i.Value.Env;
                return i.Value.LatestTags.Any(j => j.Equals(latestIndicator, StringComparison.OrdinalIgnoreCase));
            })
            .Select(i => KeyValuePair.Create(i.Value.Env, (i.Value.BuildIds.Max(), i.Value.Versions.Max()!))).ToDictionary();

        List<SemVersion> allVersionList = pairedTags.SelectMany(i => i.Value.Versions).ToList();
        Dictionary<string, List<SemVersion>> allVersionGroupDict = pairedEnvGroup.ToDictionary(i => i.Key, i => i.Value.Versions);
        Dictionary<string, SemVersion> allLatestVersions = pairedLatests.ToDictionary(i => i.Key, i => i.Value.Version);
        Dictionary<string, long> allLatestIds = pairedLatests.ToDictionary(i => i.Key, i => i.Value.BuildId);
        List<string> groupKeySorted = pairedEnvGroup.Select(i => i.Key).ToList();

        groupKeySorted.Sort();
        if (groupKeySorted.Count > 0 && groupKeySorted.First() == "")
        {
            var toMove = groupKeySorted.First();
            groupKeySorted.Remove(toMove);
            groupKeySorted.Add(toMove);
        }
        foreach (var groupKey in groupKeySorted)
        {
            var allVersion = allVersionGroupDict[groupKey];
            allVersion.Sort(SemVersion.PrecedenceComparer);
        }

        return new()
        {
            VersionList = allVersionList,
            VersionGrouped = allVersionGroupDict,
            LatestVersions = allLatestVersions,
            LatestBuildIds = allLatestIds,
            GroupKeySorted = groupKeySorted,
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

        Log.Information(headerSeparator);
        Log.Information(textHeader);
        Log.Information(headerSeparator);
        foreach (var row in rows)
        {
            if (row.All(i => i == "-"))
            {
                Log.Information(rowSeparator);
            }
            else
            {
                int rowCount = row.Count();
                string textRow = "║ ";
                for (int i = 0; i < rowCount; i++)
                {
                    string rowTemplate = "{" + i.ToString() + "}";
                    string? rowElement = row?.ElementAt(i);
                    int rowWidth = rowElement == null ? 4 : rowElement.Length;
                    textRow += columns[i].Alignment switch
                    {
                        HorizontalAlignment.Left => rowTemplate.PadLeft(columns[i].Length, rowWidth) + " ║ ",
                        HorizontalAlignment.Center => rowTemplate.PadCenter(columns[i].Length, rowWidth) + " ║ ",
                        HorizontalAlignment.Right => rowTemplate.PadRight(columns[i].Length, rowWidth) + " ║ ",
                        _ => throw new NotImplementedException()
                    };
                }
                Log.Information(textRow, row?.Select(i => i as object)?.ToArray());
            }
        }
        Log.Information(headerSeparator);
    }
}
