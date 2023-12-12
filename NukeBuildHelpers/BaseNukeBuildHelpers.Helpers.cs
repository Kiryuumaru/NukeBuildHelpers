using Nuke.Common;
using Nuke.Common.IO;
using System.Text.Json;
using NuGet.Packaging;
using System.Text.Json.Nodes;
using NukeBuildHelpers.Models;
using Semver;
using Serilog;

namespace NukeBuildHelpers;

partial class BaseNukeBuildHelpers
{
    public List<AppConfig> GetConfigs(string name)
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

    public List<AppConfig<TAppEntryConfig>> GetAppEntries<TAppEntryConfig>()
        where TAppEntryConfig : AppEntryConfig
    {
        var configs = GetConfigs("appentry.json");
        List<AppConfig<TAppEntryConfig>> newConfigs = new();
        foreach (var config in configs)
        {
            newConfigs.Add(new()
            {
                Config = JsonSerializer.Deserialize<TAppEntryConfig>(config.Json, jsonSerializerOptions),
                Json = config.Json,
                AbsolutePath = config.AbsolutePath
            });
        }
        return newConfigs;
    }

    public List<AppConfig<TAppTestConfig>> GetAppTests<TAppTestConfig>()
        where TAppTestConfig : AppTestConfig
    {
        var configs = GetConfigs("apptest.json");
        List<AppConfig<TAppTestConfig>> newConfigs = new();
        foreach (var config in configs)
        {
            newConfigs.Add(new()
            {
                Config = JsonSerializer.Deserialize<TAppTestConfig>(config.Json, jsonSerializerOptions),
                Json = config.Json,
                AbsolutePath = config.AbsolutePath
            });
        }
        return newConfigs;
    }

    public Dictionary<string, string> GetTargetArgs()
    {
        Dictionary<string, string> targetParams = new();
        if ((this as INukeBuildHelpers).Args != null)
        {
            foreach (var targetParam in (this as INukeBuildHelpers).Args.Split(';'))
            {
                if (string.IsNullOrEmpty(targetParam))
                {
                    continue;
                }
                try
                {
                    var split = targetParam.Split('=');
                    targetParams.Add(split[0], split[1]);
                }
                catch { }
            }
        }
        return targetParams;
    }

    public string GetTargetParam(string key)
    {
        var val = GetTargetArgs().GetValueOrDefault(key);
        ArgumentNullException.ThrowIfNull(val);
        return val;
    }

    public AllVersions GetCurrentVersions()
    {
        List<SemVersion> allVersionList = new();
        Dictionary<string, List<SemVersion>> allVersionGroupDict = new();
        List<string> groupKeySorted = new();
        foreach (var refs in Git.Invoke("ls-remote -t -q", logOutput: false, logInvocation: false))
        {
            string tag = refs.Text[(refs.Text.IndexOf("refs/tags/") + 10)..];
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
}
