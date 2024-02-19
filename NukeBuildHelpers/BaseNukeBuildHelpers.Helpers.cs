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

namespace NukeBuildHelpers;

partial class BaseNukeBuildHelpers
{
    private async Task PrepareAppEntries(IReadOnlyDictionary<string, (AppEntry Entry, IReadOnlyList<AppTestEntry> Tests)> appEntries)
    {
        List<Task> tasks = new();
        List<string> testAdded = new();

        foreach (var appEntry in appEntries)
        {
            tasks.Add(Task.Run(() => appEntry.Value.Entry.PrepareCore(this, OutputPath)));
            foreach (var appEntryTest in appEntry.Value.Tests)
            {
                if (testAdded.Contains(appEntryTest.Name))
                {
                    continue;
                }
                testAdded.Add(appEntryTest.Name);
                tasks.Add(Task.Run(() => appEntryTest.PrepareCore(this)));
            }
        }

        await Task.WhenAll(tasks);
    }

    private async Task TestAppEntries(IReadOnlyDictionary<string, (AppEntry Entry, IReadOnlyList<AppTestEntry> Tests)> appEntries)
    {
        List<Task> tasks = new();
        List<string> testAdded = new();

        foreach (var appEntry in appEntries)
        {
            foreach (var appEntryTest in appEntry.Value.Tests)
            {
                if (testAdded.Contains(appEntryTest.Name))
                {
                    continue;
                }
                testAdded.Add(appEntryTest.Name);
                tasks.Add(Task.Run(() => appEntryTest.RunCore(this)));
            }
        }

        await Task.WhenAll(tasks);
    }

    private async Task BuildAppEntries(IReadOnlyDictionary<string, (AppEntry Entry, IReadOnlyList<AppTestEntry> Tests)> appEntries)
    {
        List<Task> tasks = new();

        foreach (var appEntry in appEntries)
        {
            tasks.Add(Task.Run(() => appEntry.Value.Entry.BuildCore(this, OutputPath)));
        }

        await Task.WhenAll(tasks);
    }

    private async Task PackAppEntries(IReadOnlyDictionary<string, (AppEntry Entry, IReadOnlyList<AppTestEntry> Tests)> appEntries)
    {
        List<Task> tasks = new();

        foreach (var appEntry in appEntries)
        {
            tasks.Add(Task.Run(() => appEntry.Value.Entry.PackCore(this, OutputPath)));
        }

        await Task.WhenAll(tasks);
    }

    private async Task ReleaseAppEntries(IReadOnlyDictionary<string, (AppEntry Entry, IReadOnlyList<AppTestEntry> Tests)> appEntries)
    {
        List<Task> tasks = new();

        foreach (var appEntry in appEntries)
        {
            tasks.Add(Task.Run(() => appEntry.Value.Entry.ReleaseCore(this, OutputPath)));
        }

        await Task.WhenAll(tasks);
    }

    private static List<AppEntry> GetAppEntries()
    {
        var asmNames = DependencyContext.Default.GetDefaultAssemblyNames();

        var allTypes = asmNames.Select(Assembly.Load)
            .SelectMany(t => t.GetTypes())
            .Where(p => p.GetTypeInfo().IsSubclassOf(typeof(AppEntry)) && !p.ContainsGenericParameters);

        List<AppEntry> entry = new();
        foreach (Type type in allTypes)
        {
            entry.Add(Activator.CreateInstance(type) as AppEntry);
        }
        return entry;
    }

    private static List<AppTestEntry> GetAppTestEntries()
    {
        var asmNames = DependencyContext.Default.GetDefaultAssemblyNames();

        var allTypes = asmNames.Select(Assembly.Load)
            .SelectMany(t => t.GetTypes())
            .Where(p => p.GetTypeInfo().IsSubclassOf(typeof(AppTestEntry)) && !p.ContainsGenericParameters);

        List<AppTestEntry> entry = new();
        foreach (Type type in allTypes)
        {
            entry.Add(Activator.CreateInstance(type) as AppTestEntry);
        }
        return entry;
    }

    private static IReadOnlyDictionary<string, (AppEntry Entry, IReadOnlyList<AppTestEntry> Tests)> GetAppEntryConfigs()
    {
        Dictionary<string, (AppEntry Entry, IReadOnlyList<AppTestEntry> Tests)> configs = new();

        bool hasMainReleaseEntry = false;
        List<AppEntry> appEntries = new();
        foreach (var appEntry in GetAppEntries())
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

        List<(bool IsAdded, AppTestEntry AppTestEntry)> appTestEntries = new();
        foreach (var appTestEntry in GetAppTestEntries())
        {
            if (!appTestEntry.Enable)
            {
                continue;
            }
            if (appTestEntry.AppEntryTargets == null || !appTestEntry.AppEntryTargets.Any())
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
            List<AppTestEntry> appTestEntriesFound = new();
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

    private AllVersions GetAllVersions(string appId, IReadOnlyDictionary<string, (AppEntry Entry, IReadOnlyList<AppTestEntry> Tests)> appEntryConfigs, ref IReadOnlyCollection<Output> lsRemoteOutput)
    {
        GetOrFail(appId, appEntryConfigs, out _, out var appEntry);
        List<SemVersion> allVersionList = new();
        Dictionary<string, List<SemVersion>> allVersionGroupDict = new();
        Dictionary<string, SemVersion> allLatestVersions = new();
        List<string> groupKeySorted = new();
        Dictionary<string, string> latestVersionCommitId = new();
        string basePeel = "refs/tags/";
        lsRemoteOutput ??= Git.Invoke("ls-remote -t -q", logOutput: false, logInvocation: false);
        foreach (var refs in lsRemoteOutput)
        {
            string rawTag = refs.Text[(refs.Text.IndexOf(basePeel) + basePeel.Length)..];
            string tag;
            string commitId = refs.Text[0..refs.Text.IndexOf(basePeel)].Trim();

            if (appEntry.Entry.MainRelease)
            {
                tag = rawTag;
            }
            else if (rawTag.StartsWith(appId.ToLowerInvariant()))
            {
                tag = rawTag[(rawTag.IndexOf(appId.ToLowerInvariant()) + appId.Length + 1)..];
            }
            else
            {
                continue;
            }
            if (tag.ToLowerInvariant().StartsWith("latest"))
            {
                latestVersionCommitId[rawTag] = commitId;
            }
        }
        foreach (var refs in lsRemoteOutput)
        {
            string rawTag = refs.Text[(refs.Text.IndexOf(basePeel) + basePeel.Length)..];
            string tag;
            string commitId = refs.Text[0..refs.Text.IndexOf(basePeel)].Trim();

            if (appEntry.Entry.MainRelease)
            {
                tag = rawTag;
            }
            else if (rawTag.StartsWith(appId.ToLowerInvariant()))
            {
                tag = rawTag[(rawTag.IndexOf(appId.ToLowerInvariant()) + appId.Length + 1)..];
            }
            else
            {
                continue;
            }
            if (!SemVersion.TryParse(tag, SemVersionStyles.Strict, out SemVersion tagSemver))
            {
                continue;
            }

            string env = tagSemver.IsPrerelease ? tagSemver.PrereleaseIdentifiers[0].Value.ToLowerInvariant() : "";
            string latestIndicator = env == "" ? "latest" : "latest-" + env;

            if (!appEntry.Entry.MainRelease)
            {
                latestIndicator = appId.ToLowerInvariant() + "/" + latestIndicator;
            }
            if (latestVersionCommitId.TryGetValue(latestIndicator, out var val) && val == commitId)
            {
                Console.WriteLine("Latest for " + latestIndicator + " is: " + tag);
            }
            if (allVersionGroupDict.TryGetValue(env, out List<SemVersion> versions))
            {
                versions.Add(tagSemver);
            }
            else
            {
                versions = new() { tagSemver };
                allVersionGroupDict.Add(env, versions);
                groupKeySorted.Add(env);
            }
            allVersionList.Add(tagSemver);
        }
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
            GroupKeySorted = groupKeySorted,
        };
    }

    private static void GetOrFail<T>(Func<T> valFactory, out T valOut)
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

    private static void GetOrFail(string appId, IReadOnlyDictionary<string, (AppEntry Entry, IReadOnlyList<AppTestEntry> Tests)> appEntryConfigs, out string appIdOut, out (AppEntry Entry, IReadOnlyList<AppTestEntry> Tests) appEntryConfig)
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

    private static void GetOrFail(string rawValue, out SemVersion valOut)
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

    private static void LogInfoTable(IEnumerable<(string Text, HorizontalAlignment Alignment)> headers, params IEnumerable<string>[] rows)
    {
        List<(int Length, string Text, HorizontalAlignment Alignment)> columns = new();

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

        string rowSeparator = "╬";
        string textHeader = "║";
        foreach (var (Length, Text, AlignRight) in columns)
        {
            rowSeparator += new string('═', Length + 2) + '╬';
            textHeader += Text.PadCenter(Length + 2) + '║';
        }

        Log.Information(rowSeparator);
        Log.Information(textHeader);
        Log.Information(rowSeparator);
        foreach (var row in rows)
        {
            int rowCount = row.Count();
            string textRow = "║ ";
            for (int i = 0; i < rowCount; i++)
            {
                string rowTemplate = "{" + i.ToString() + "}";
                string rowElement = row.ElementAt(i);
                int rowWidth = rowElement == null ? 4 : rowElement.Length;
                textRow += columns[i].Alignment switch
                {
                    HorizontalAlignment.Left => rowTemplate.PadLeft(columns[i].Length, rowWidth) + " ║ ",
                    HorizontalAlignment.Center => rowTemplate.PadCenter(columns[i].Length, rowWidth) + " ║ ",
                    HorizontalAlignment.Right => rowTemplate.PadRight(columns[i].Length, rowWidth) + " ║ ",
                    _ => throw new NotImplementedException()
                };
            }
            Log.Information(textRow, row.Select(i => i as object).ToArray());
        }
        Log.Information(rowSeparator);
    }
}
