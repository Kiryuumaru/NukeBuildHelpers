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

namespace NukeBuildHelpers;

partial class BaseNukeBuildHelpers
{
    private static readonly JsonSerializerOptions _jsonSnakeCaseNamingOption = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static readonly JsonSerializerOptions _jsonSnakeCaseNamingOptionIndented = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

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

                    Log.Information("heere3 {asc} {csa} {ss}" + secret.SecretHelper.Name, secret.MemberInfo.Name, secretValue);

                    if (secret.MemberInfo is PropertyInfo prop)
                    {
                        prop.SetValue(appEntry.Value.Entry, secretValue);
                    }
                    else if (secret.MemberInfo is FieldInfo field)
                    {
                        field.SetValue(appEntry.Value.Entry, secretValue);
                    }
                }
            }

            appEntry.Value.Entry.NukeBuild = this;
            appEntry.Value.Entry.OutputPath = OutputPath;
            foreach (var appTestEntry in appEntry.Value.Tests)
            {
                if (appTestEntrySecretMap.TryGetValue(appEntry.Value.Entry.Id, out var testSecretMap) &&
                    appSecretMap.EntryType == appEntry.Value.Entry.GetType())
                {
                    foreach (var secret in testSecretMap.SecretHelpers)
                    {
                        var secretValue = Environment.GetEnvironmentVariable(secret.SecretHelper.Name);

                        Log.Information("heerscscse3 {asc} {csa} {ss}" + secret.SecretHelper.Name, secret.MemberInfo.Name, secretValue);

                        if (secret.MemberInfo is PropertyInfo prop)
                        {
                            prop.SetValue(appTestEntry, secretValue);
                        }
                        else if (secret.MemberInfo is FieldInfo field)
                        {
                            field.SetValue(appTestEntry, secretValue);
                        }
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
                        };
                    }
                }
            }
        }
    }

    private async Task TestAppEntries(Dictionary<string, (AppEntry Entry, List<AppTestEntry> Tests)> appEntries, IEnumerable<string> idsToRun, PreSetupOutput? preSetupOutput)
    {
        List<Task> parallels = [];
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
                    parallels.Add(Task.Run(() => appEntryTest.Run()));
                }
                else
                {
                    nonParallels.Add(() => appEntryTest.Run());
                }
            }
        }

        foreach (var nonParallel in nonParallels)
        {
            await Task.Run(nonParallel);
        }

        await Task.WhenAll(parallels);
    }

    private async Task BuildAppEntries(Dictionary<string, (AppEntry Entry, List<AppTestEntry> Tests)> appEntries, IEnumerable<string> idsToRun, PreSetupOutput? preSetupOutput)
    {
        List<Task> parallels = [];
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
                parallels.Add(Task.Run(() => appEntry.Value.Entry.Build()));
            }
            else
            {
                nonParallels.Add(() => appEntry.Value.Entry.Build());
            }
        }

        foreach (var nonParallel in nonParallels)
        {
            await Task.Run(nonParallel);
        }

        await Task.WhenAll(parallels);
    }

    private async Task PublishAppEntries(Dictionary<string, (AppEntry Entry, List<AppTestEntry> Tests)> appEntries, IEnumerable<string> idsToRun, PreSetupOutput? preSetupOutput)
    {
        List<Task> parallels = [];
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
                parallels.Add(Task.Run(() => appEntry.Value.Entry.Publish()));
            }
            else
            {
                nonParallels.Add(() => appEntry.Value.Entry.Publish());
            }
        }

        foreach (var nonParallel in nonParallels)
        {
            await Task.Run(nonParallel);
        }

        await Task.WhenAll(parallels);
    }

    private static List<T> GetEntries<T>()
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

    private static Dictionary<string, (AppEntry Entry, List<AppTestEntry> Tests)> GetAppEntryConfigs()
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

    private static Dictionary<string, (Type EntryType, List<(MemberInfo MemberInfo, SecretHelperAttribute SecretHelper)> SecretHelpers)> GetEntrySecretMap<T>()
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
        List<SemVersion> allVersionList = [];
        Dictionary<string, List<SemVersion>> allVersionGroupDict = [];
        Dictionary<string, SemVersion> allLatestVersions = [];
        List<string> groupKeySorted = [];
        Dictionary<string, string> latestVersionCommitId = [];
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
            else if (rawTag.StartsWith(appId, StringComparison.InvariantCultureIgnoreCase))
            {
                tag = rawTag[(rawTag.IndexOf(appId, StringComparison.InvariantCultureIgnoreCase) + appId.Length + 1)..];
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
                allLatestVersions[env] = tagSemver;
            }
            if (allVersionGroupDict.TryGetValue(env, out List<SemVersion>? versions))
            {
                versions.Add(tagSemver);
            }
            else
            {
                versions = [tagSemver];
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

    private static void GetOrFail(string appId, Dictionary<string, (AppEntry Entry, List<AppTestEntry> Tests)> appEntryConfigs, out string appIdOut, out (AppEntry Entry, List<AppTestEntry> Tests) appEntryConfig)
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

    private static void GetOrFail(string? rawValue, out SemVersion valOut)
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
        Log.Information(rowSeparator);
    }
}
