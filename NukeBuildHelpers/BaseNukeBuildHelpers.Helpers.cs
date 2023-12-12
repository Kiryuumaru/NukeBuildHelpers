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

    public Dictionary<string, string> GetTargetParams()
    {
        Dictionary<string, string> targetParams = new();
        if ((this as INukeBuildHelpers).TargetParams != null)
        {
            foreach (var targetParam in (this as INukeBuildHelpers).TargetParams.Split(';'))
            {
                if (string.IsNullOrEmpty(targetParam))
                {
                    continue;
                }
                var split = targetParam.Split('=');
                targetParams.Add(split[0], split[1]);
            }
        }
        return targetParams;
    }

    public string GetTargetParam(string key)
    {
        var val = GetTargetParams().GetValueOrDefault(key);
        ArgumentNullException.ThrowIfNull(val);
        return val;
    }

    public void BumpRelease(string bump)
    {
        if (string.IsNullOrEmpty(bump))
        {
            Assert.Fail("bump value is empty");
            return;
        }

        var bumpValLower = bump.ToLowerInvariant();
        bool isMainBump = false;
        if (bumpValLower == "major" || bumpValLower == "minor" || bumpValLower == "patch")
        {
            isMainBump = true;
        }

        Dictionary<string, List<SemVersion>> allVersionsDict = new();
        List<KeyValuePair<string, List<SemVersion>>> allVersionsList = new();
        foreach (var refs in Git.Invoke("ls-remote -t", logOutput: false, logInvocation: false))
        {
            string tag = refs.Text[(refs.Text.IndexOf("refs/tags/") + 10)..];
            if (!SemVersion.TryParse(tag, SemVersionStyles.Strict, out SemVersion tagSemver))
            {
                continue;
            }
            string env = tagSemver.IsPrerelease ? tagSemver.PrereleaseIdentifiers[0].Value.ToLowerInvariant() : "";
            if (allVersionsDict.TryGetValue(env, out List<SemVersion> versions))
            {
                versions.Add(tagSemver);
            }
            else
            {
                versions = new() { tagSemver };
                allVersionsDict.Add(env, versions);
                allVersionsList.Add(new(env, versions));
            }
        }
        allVersionsList = allVersionsList.OrderBy(i => i.Key).ToList();
        if (allVersionsList.Count > 0 && allVersionsList.First().Key == "")
        {
            var toMove = allVersionsList.First();
            allVersionsList.Remove(toMove);
            allVersionsList.Add(toMove);
        }
        foreach (var allVersion in allVersionsList)
        {
            allVersion.Value.Sort(SemVersion.PrecedenceComparer);
            var last = allVersion.Value.Last();
            if (string.IsNullOrEmpty(allVersion.Key))
            {
                Log.Information("Current main releases is {currentVersion}", last);
            }
            else
            {
                Log.Information("Current {env} is {currentVersion}", allVersion.Key, last);
            }
        }

        SemVersion versionToBump = null;

        if (versionToBump.IsPrerelease)
        {
            Log.Information("Version to bump is {versionToBump} to {env}", versionToBump, versionToBump.PrereleaseIdentifiers[0].Value.ToLowerInvariant());
        }
        else
        {
            Log.Information("Version to bump is {versionToBump} to main releases", versionToBump);
        }

        SemVersion currentVersion = new(0);
        foreach (var tag in Git.Invoke("tag -l", logOutput: false, logInvocation: false))
        {
            if (!SemVersion.TryParse(tag.Text, SemVersionStyles.Strict, out SemVersion tagSemver))
            {
                continue;
            }
            if (string.IsNullOrEmpty(bump))
            {
                if (tagSemver.IsPrerelease)
                {
                    continue;
                }
            }
            else
            {
                if (!tagSemver.IsPrerelease)
                {
                    continue;
                }
                if (bump.ToLowerInvariant() != tagSemver.PrereleaseIdentifiers[0].Value.ToLowerInvariant())
                {
                    continue;
                }
            }
            if (SemVersion.ComparePrecedence(currentVersion, tagSemver) > 0)
            {
                continue;
            }
            currentVersion = tagSemver;
        }
    }
}
