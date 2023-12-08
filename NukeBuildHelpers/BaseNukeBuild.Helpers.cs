using Nuke.Common;
using Nuke.Common.IO;
using System.Text.Json;
using NuGet.Packaging;
using System.Text.Json.Nodes;
using NukeBuildHelpers.Models;

namespace NukeBuildHelpers;

partial class BaseNukeBuild : NukeBuild
{
    protected static List<(JsonNode Json, AbsolutePath AbsolutePath)> GetConfigs(string name)
    {
        List<(JsonNode Json, AbsolutePath AbsolutePath)> configs = new();
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
            configs.AddRange(newConfigs.Select(c => (c, configPath)));
        }
        return configs;
    }

    protected static List<CIConfig<AppEntry>> GetAppEntries()
    {
        var configs = GetConfigs("appentry.json");
        List<CIConfig<AppEntry>> newConfigs = new();
        foreach (var config in configs)
        {
            newConfigs.Add(new()
            {
                Config = JsonSerializer.Deserialize<AppEntry>(config.Json, jsonSerializerOptions),
                Json = config.Json,
                AbsolutePath = config.AbsolutePath
            });
        }
        return newConfigs;
    }

    protected static List<CIConfig<AppTestEntry>> GetAppTestEntries()
    {
        var configs = GetConfigs("apptestentry.json");
        List<CIConfig<AppTestEntry>> newConfigs = new();
        foreach (var config in configs)
        {
            newConfigs.Add(new()
            {
                Config = JsonSerializer.Deserialize<AppTestEntry>(config.Json, jsonSerializerOptions),
                Json = config.Json,
                AbsolutePath = config.AbsolutePath
            });
        }
        return newConfigs;
    }
}
