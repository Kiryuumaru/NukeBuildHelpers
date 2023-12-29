using Nuke.Common;
using Nuke.Common.IO;
using System.Text.Json;
using NuGet.Packaging;
using System.Text.Json.Nodes;
using NukeBuildHelpers.Models;
using Semver;
using Serilog;
using YamlDotNet.Core.Tokens;

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

    private IReadOnlyDictionary<string, (AppConfig<AppEntryConfig> Entry, IReadOnlyList<AppConfig<AppTestEntryConfig>> Tests)> appEntryConfigs;
    private IReadOnlyDictionary<string, (AppConfig<AppEntryConfig> Entry, IReadOnlyList<AppConfig<AppTestEntryConfig>> Tests)> GetAppEntryConfigs(bool cache = true)
    {
        if (appEntryConfigs == null || !cache)
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

            appEntryConfigs = configs;
        }

        return appEntryConfigs;
    }

    private AllVersions allVersions;
    private AllVersions GetAllVersions(string appId, IReadOnlyDictionary<string, (AppConfig<AppEntryConfig> Entry, IReadOnlyList<AppConfig<AppTestEntryConfig>> Tests)> appEntryConfigs, bool cache = true)
    {
        if (allVersions == null || !cache)
        {
            GetOrFail(appId, appEntryConfigs, out _, out var appEntry);
            List<SemVersion> allVersionList = new();
            Dictionary<string, List<SemVersion>> allVersionGroupDict = new();
            List<string> groupKeySorted = new();
            foreach (var refs in Git.Invoke("ls-remote -t -q", logOutput: false, logInvocation: false))
            {
                string peel;
                if (appEntry.Entry.Config.MainRelease)
                {
                    peel = "refs/tags/";
                }
                else if (refs.Text.StartsWith($"refs/tags/{appId.ToLowerInvariant()}"))
                {
                    peel = $"refs/tags/{appId.ToLowerInvariant()}";
                }
                else
                {
                    continue;
                }
                string tag = refs.Text[(refs.Text.IndexOf(peel) + peel.Length)..];
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
            allVersions = new()
            {
                VersionList = allVersionList,
                VersionGrouped = allVersionGroupDict,
                GroupKeySorted = groupKeySorted,
            };
        }

        return allVersions;
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
}
