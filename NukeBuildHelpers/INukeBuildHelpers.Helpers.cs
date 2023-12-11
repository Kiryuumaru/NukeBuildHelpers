using Nuke.Common;
using Nuke.Common.IO;
using System.Text.Json;
using NuGet.Packaging;
using System.Text.Json.Nodes;
using NukeBuildHelpers.Models;

namespace NukeBuildHelpers;

partial interface INukeBuildHelpers : INukeBuild
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
}
