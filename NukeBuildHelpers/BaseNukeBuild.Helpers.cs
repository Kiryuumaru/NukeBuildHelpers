using System;
using System.Linq;
using Serilog;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using Nuke.Common.Git;
using System.Collections.Generic;
using System.Text.Json;
using System.IO;
using NuGet.Packaging;
using System.Text.Json.Nodes;
using NukeBuildHelpers.Models;
using NukeBuildHelpers.Enums;
using System.Xml.Linq;

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
        var configs = GetConfigs("appentry.json");
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
