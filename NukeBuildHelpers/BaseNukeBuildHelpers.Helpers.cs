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

    public AllVersions GetCurrentVersions()
    {
        List<SemVersion> allVersionList = new();
        Dictionary<string, List<SemVersion>> allVersionGroupDict = new();
        List<string> groupKeySorted = new();
        foreach (var refs in Git.Invoke("ls-remote -t", logOutput: false, logInvocation: false))
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

    public void BumpRelease(IDictionary<string, int> bumps)
    {
        var currentVersions = GetCurrentVersions();

        foreach (var groupKey in currentVersions.GroupKeySorted)
        {
            if (string.IsNullOrEmpty(groupKey))
            {
                Log.Information("Current main releases is {currentVersion}", currentVersions.VersionGrouped[groupKey].Last());
            }
            else
            {
                Log.Information("Current {env} is {currentVersion}", groupKey, currentVersions.VersionGrouped[groupKey].Last());
            }
        }

        if (!bumps.Any())
        {
            Assert.Fail("bump is empty");
            return;
        }

        bool hasMainBump = false;
        bool hasPrerelBump = false;
        KeyValuePair<string, int>? majorBump = null;
        KeyValuePair<string, int>? minorBump = null;
        KeyValuePair<string, int>? patchBump = null;
        KeyValuePair<string, int>? prerelBump = null;
        foreach (var bump in bumps)
        {
            if (bump.Key == "major")
            {
                majorBump = bump;
                hasMainBump = true;
            }
            else if (bump.Key == "minor")
            {
                minorBump = bump;
                hasMainBump = true;
            }
            else if (bump.Key == "patch")
            {
                patchBump = bump;
                hasMainBump = true;
            }
            else
            {
                if (prerelBump.HasValue)
                {
                    Assert.Fail("multiple prerel bumps");
                    return;
                }
                prerelBump = bump;
                hasPrerelBump = true;
            }
        }

        foreach (var bump in bumps)
        {
            Log.Information("Bump {env} with {val}", bump.Key, "+" + bump.Value.ToString());
        }

        SemVersion versionToBump = null;
        if (hasPrerelBump)
        {
            if (currentVersions.VersionGrouped.ContainsKey(prerelBump.Value.Key))
            {
                versionToBump = currentVersions.VersionGrouped[prerelBump.Value.Key].Last();
            }
            else
            {
                versionToBump = null;
            }
        }
        else
        {
            if (currentVersions.VersionGrouped.ContainsKey(""))
            {
                versionToBump = currentVersions.VersionGrouped[""].Last();
            }
            else
            {
                versionToBump = null;
            }
        }

        if (majorBump.HasValue)
        {
            versionToBump = versionToBump.WithMajor(versionToBump.Major + majorBump.Value.Value);
            versionToBump = versionToBump.WithMinor(0);
            versionToBump = versionToBump.WithPatch(0);
        }
        
        if (minorBump.HasValue)
        {
            versionToBump = versionToBump.WithMinor(versionToBump.Minor + minorBump.Value.Value);
            versionToBump = versionToBump.WithPatch(0);
        }
        
        if (patchBump.HasValue)
        {
            versionToBump = versionToBump.WithPatch(versionToBump.Patch + patchBump.Value.Value);
        }

        if (prerelBump.HasValue)
        {
            var prerelNum = ((versionToBump.PrereleaseIdentifiers.Count > 1 ? int.Parse(versionToBump.PrereleaseIdentifiers[1]) : 0) + prerelBump.Value.Value).ToString();
            versionToBump = versionToBump.WithPrerelease(prerelBump.Value.Key, prerelNum);
        }

        if (versionToBump.IsPrerelease)
        {
            Log.Information("Calculated bump {env} release to {versionToBump}", versionToBump.PrereleaseIdentifiers[0].Value.ToLowerInvariant(), versionToBump);
        }
        else
        {
            Log.Information("Calculated bump main release to {versionToBump}", versionToBump);
        }
    }
}
