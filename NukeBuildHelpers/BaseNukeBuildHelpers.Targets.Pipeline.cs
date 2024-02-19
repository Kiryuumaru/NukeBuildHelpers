using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using NukeBuildHelpers.Models;
using Octokit;
using Semver;
using Serilog;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NukeBuildHelpers;

partial class BaseNukeBuildHelpers
{
    private AbsolutePath _generatedVersionPath = OutputPath / "generated_version.json";

    public Target GenerateVersionFiles => _ => _
        .Description("Generates version files from tags, with --args \"{appid}\"")
        .Executes(() => {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntryConfigs);

            foreach (var key in splitArgs.Keys.Any() ? splitArgs.Keys.ToList() : appEntryConfigs.Select(i => i.Key))
            {
                string appId = key;

                GetOrFail(appId, appEntryConfigs, out appId, out var appEntry);
                GetOrFail(() => GetAllVersions(appId, appEntryConfigs), out var allVersions);

                JsonArray jsonArray = new();

                if (allVersions.GroupKeySorted.Any())
                {
                    foreach (var groupKey in allVersions.GroupKeySorted)
                    {
                        string env;
                        if (string.IsNullOrEmpty(groupKey))
                        {
                            env = "main";
                        }
                        else
                        {
                            env = groupKey;
                        }
                        jsonArray.Add(JsonNode.Parse($$"""
                            {
                                "appid": "{{appId}}",
                                "version": "{{allVersions.VersionGrouped[groupKey].Last()}}"
                            }
                            """));
                    }
                }

                var generatedVersion = JsonSerializer.Serialize(jsonArray, new JsonSerializerOptions { WriteIndented = true });

                Console.WriteLine("Version file generated: \n" + generatedVersion);

                File.WriteAllText(_generatedVersionPath.ToString(), generatedVersion);

                Console.WriteLine("Saved to: " + _generatedVersionPath.ToString());
            }
        });

    public Target ConsumeBuild => _ => _
        .Description("Consume build, with --args \"{appid}\"")
        .DependsOn(GenerateVersionFiles)
        .Executes(() =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntryConfigs);

            var generatedVersion = JsonSerializer.Deserialize<JsonArray>(File.ReadAllText(_generatedVersionPath.ToString()));

            List<(string Appid, string Version)> versions = new();
            foreach (var version in generatedVersion)
            {
                versions.Add((version["appid"].ToString(), version["version"].ToString()));
            }

        });

    public Target GithubPublish => _ => _
        .Description("Generates app test entry template, with --args \"path={path}\"")
        .Executes(() => {
            GetOrFail(() => SplitArgs, out var splitArgs);

            splitArgs.TryGetValue("path", out string pathRaw);

            AbsolutePath absolutePath = RootDirectory / "apptestentry.sample.json";
            if (!string.IsNullOrEmpty(pathRaw))
            {
                absolutePath = AbsolutePath.Create(absolutePath);
            }

            Log.Information("Generating app config to \"{path}\"", absolutePath);

            //AppTestEntryConfig config = new()
            //{
            //    BuildsOn = Enums.BuildsOnType.Ubuntu2204
            //};

            //File.WriteAllText(absolutePath, JsonSerializer.Serialize(config, jsonSerializerOptions));

            Log.Information("Generate done");
        });
}
