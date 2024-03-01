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

            JsonArray jsonArray = new();

            IReadOnlyCollection<Output> lsRemote = null;

            foreach (var key in splitArgs.Keys.Any() ? splitArgs.Keys.ToList() : appEntryConfigs.Select(i => i.Key))
            {
                string appId = key;

                GetOrFail(appId, appEntryConfigs, out appId, out var appEntry);
                GetOrFail(() => GetAllVersions(appId, appEntryConfigs, ref lsRemote), out var allVersions);


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
                        var currentVersion = allVersions.VersionGrouped[groupKey].Last();
                        var releasedVersion = allVersions.LatestVersions[groupKey];
                        bool hasRelease = currentVersion != releasedVersion;
                        jsonArray.Add(JsonNode.Parse($$"""
                            {
                                "appid": "{{appId}}",
                                "current": "{{currentVersion}}",
                                "released": "{{releasedVersion}}",
                                "hasRelease": {{hasRelease.ToString().ToLowerInvariant()}}
                            }
                            """));
                    }
                }
            }

            var generatedVersion = JsonSerializer.Serialize(jsonArray, new JsonSerializerOptions { WriteIndented = true });

            Log.Information("Version file generated: \n" + generatedVersion);

            File.WriteAllText(_generatedVersionPath.ToString(), generatedVersion);

            Log.Information("Saved to: " + _generatedVersionPath.ToString());
        });

    public Target ConsumeBuild => _ => _
        .Description("Consume build, with --args \"{appid}\"")
        .DependsOn(GenerateVersionFiles)
        .Executes(() =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntryConfigs);

            var generatedVersion = JsonSerializer.Deserialize<JsonArray>(File.ReadAllText(_generatedVersionPath.ToString()));

            List<(string Appid, string CurrentVersion, string ReleasedVersion, bool HasRelease)> versions = new();
            foreach (var version in generatedVersion)
            {
                versions.Add((
                    version["appid"].ToString(),
                    version["current"].ToString(),
                    version["released"].ToString(),
                    version["hasRelease"].GetValue<bool>()
                    ));
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
            //    RunsOn = Enums.RunsOnType.Ubuntu2204
            //};

            //File.WriteAllText(absolutePath, JsonSerializer.Serialize(config, jsonSerializerOptions));

            Log.Information("Generate done");
        });
}
