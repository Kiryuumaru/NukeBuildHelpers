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
using static System.Net.Mime.MediaTypeNames;
using System.Runtime.CompilerServices;

namespace NukeBuildHelpers;

partial class BaseNukeBuildHelpers
{
    private static List<AppConfig> GetConfigs(string name)
    {
        List<AppConfig> configs = new();
        foreach (var configPath in RootDirectory.GetFiles(name, 10))
        {
            var configJson = File.ReadAllText(configPath);
            List<JsonNode> newConfigs = new();
            JsonNode node = JsonNode.Parse(configJson);
            if (node is JsonArray arr)
            {
                foreach (var n in arr)
                {
                    newConfigs.Add(n);
                }
            }
            else
            {
                newConfigs.Add(node);
            }
            configs.AddRange(newConfigs.Select(c => new AppConfig()
            {
                Json = c,
                AbsolutePath = configPath
            }));
        }
        return configs;
    }

    private static IReadOnlyDictionary<string, (AppConfig<AppEntryConfig> Entry, IReadOnlyList<AppConfig<AppTestEntryConfig>> Tests)> GetAppEntryConfigs()
    {
        Dictionary<string, (AppConfig<AppEntryConfig> Entry, IReadOnlyList<AppConfig<AppTestEntryConfig>> Tests)> configs = new();

        bool hasMainReleaseEntry = false;
        List<AppConfig<AppEntryConfig>> appEntries = new();
        foreach (var config in GetConfigs("appentry*.json"))
        {
            var appEntryConfig = JsonSerializer.Deserialize<AppEntryConfig>(config.Json, jsonSerializerOptions);
            if (!appEntryConfig.Enable)
            {
                continue;
            }
            if (appEntryConfig.MainRelease)
            {
                if (hasMainReleaseEntry)
                {
                    throw new Exception("Contains multiple main release app entry");
                }
                hasMainReleaseEntry = true;
            }
            if (string.IsNullOrEmpty(appEntryConfig.Id))
            {
                throw new Exception($"App entry contains null or empty id \"{config.AbsolutePath}\"");
            }
            appEntries.Add(new()
            {
                Config = appEntryConfig,
                Json = config.Json,
                AbsolutePath = config.AbsolutePath
            });
        }

        List<AppConfig<AppTestEntryConfig>> appTestEntries = new();
        foreach (var config in GetConfigs("apptestentry*.json"))
        {
            var appTestEntryConfig = JsonSerializer.Deserialize<AppTestEntryConfig>(config.Json, jsonSerializerOptions);
            if (!appTestEntryConfig.Enable)
            {
                continue;
            }
            if (string.IsNullOrEmpty(appTestEntryConfig.AppEntryId))
            {
                throw new Exception($"App test entry contains null or empty app entry id \"{config.AbsolutePath}\"");
            }
            appTestEntries.Add(new()
            {
                Config = appTestEntryConfig,
                Json = config.Json,
                AbsolutePath = config.AbsolutePath
            });
        }

        foreach (var appEntry in appEntries)
        {
            if (configs.ContainsKey(appEntry.Config.Id))
            {
                throw new Exception($"Contains multiple app entry id \"{appEntry.Config.Id}\"");
            }
            var appTestEntriesFound = appTestEntries
                .Where(at => at.Config.AppEntryId == appEntry.Config.Id)
                .ToList()
                .AsReadOnly();
            configs.Add(appEntry.Config.Id, (appEntry, appTestEntriesFound));
            appTestEntries.RemoveAll(at => at.Config.AppEntryId == appEntry.Config.Id);
        }

        if (appTestEntries.Count > 0)
        {
            foreach (var appTestEntry in appTestEntries)
            {
                Assert.Fail($"App entry id \"{appTestEntry.Config.AppEntryId}\" does not exist, from app test entry \"{appTestEntry.AbsolutePath}\"");
            }
            throw new Exception("Some app test entry has non-existence app entry id");
        }

        return configs;
    }

    private AllVersions GetAllVersions(string appId, IReadOnlyDictionary<string, (AppConfig<AppEntryConfig> Entry, IReadOnlyList<AppConfig<AppTestEntryConfig>> Tests)> appEntryConfigs)
    {
        GetOrFail(appId, appEntryConfigs, out _, out var appEntry);
        List<SemVersion> allVersionList = new();
        Dictionary<string, List<SemVersion>> allVersionGroupDict = new();
        List<string> groupKeySorted = new();
        string basePeel = "refs/tags/";
        foreach (var refs in Git.Invoke("ls-remote -t -q", logOutput: false, logInvocation: false))
        {
            string rawTag = refs.Text[(refs.Text.IndexOf(basePeel) + basePeel.Length)..];
            string tag;
            if (appEntry.Entry.Config.MainRelease)
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

    private static void GetOrFail(string appId, IReadOnlyDictionary<string, (AppConfig<AppEntryConfig> Entry, IReadOnlyList<AppConfig<AppTestEntryConfig>> Tests)> appEntryConfigs, out string appIdOut, out (AppConfig<AppEntryConfig> Entry, IReadOnlyList<AppConfig<AppTestEntryConfig>> Tests) appEntryConfig)
    {
        try
        {
            // Fail if appId is null and solution has multiple app entries
            if (string.IsNullOrEmpty(appId) && !appEntryConfigs.Any(ae => ae.Value.Entry.Config.MainRelease))
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
                appEntryConfig = appEntryConfigs.Where(ae => ae.Value.Entry.Config.MainRelease).First().Value;
            }

            appIdOut = appEntryConfig.Entry.Config.Id;
        }
        catch (Exception ex)
        {
            Assert.Fail(ex.Message, ex);
            throw;
        }
    }

    private static void GetOrFail(string rawValue, out bool valOut)
    {
        try
        {
            valOut = rawValue?.ToLowerInvariant() switch
            {
                "1" => true,
                "0" => false,
                "true" => true,
                "false" => false,
                "yes" => true,
                "no" => false,
                "" => true,
                null => true,
                _ => throw new ArgumentException($"Invalid boolean value \"{rawValue}\""),
            };
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
            for (int i = 0; i < row.Count(); i++)
            {
                var rowElement = row.ElementAt(i);
                columns[i] = (MathExtensions.Max(rowCount, columns[i].Length, rowElement.Length), columns[i].Text, columns[i].Alignment);
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
            string textRow = "║ ";
            for (int i = 0; i < row.Count(); i++)
            {
                string rowTemplate = "{" + i.ToString() + "}";
                string rowElement = row.ElementAt(i);
                string inCell = columns[i].Alignment switch
                {
                    HorizontalAlignment.Left => rowElement.PadLeft(columns[i].Length),
                    HorizontalAlignment.Center => rowElement.PadCenter(columns[i].Length),
                    HorizontalAlignment.Right => rowElement.PadRight(columns[i].Length),
                    _ => throw new NotImplementedException()
                };
                textRow += inCell.Replace(rowElement, rowTemplate) + " ║ ";
            }
            Log.Information(textRow, row.Select(i => i as object).ToArray());
        }
        Log.Information(rowSeparator);
    }
}
