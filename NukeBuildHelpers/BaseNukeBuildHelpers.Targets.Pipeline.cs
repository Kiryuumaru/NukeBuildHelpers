using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.DependencyModel;
using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Models;
using Octokit;
using Semver;
using Serilog;
using Serilog.Events;
using System.Reflection;
using System.Text.Json;
using YamlDotNet.Core.Tokens;

namespace NukeBuildHelpers;

partial class BaseNukeBuildHelpers
{
    private static PreSetupOutput GetPreSetupOutput()
    {
        string? preSetupOutputValue = Environment.GetEnvironmentVariable("PRE_SETUP_OUTPUT");

        if (string.IsNullOrEmpty(preSetupOutputValue))
        {
            throw new Exception("PRE_SETUP_OUTPUT is empty");
        }

        PreSetupOutput? preSetupOutput = JsonSerializer.Deserialize<PreSetupOutput>(preSetupOutputValue, _jsonSnakeCaseNamingOption);

        return preSetupOutput ?? throw new Exception("PRE_SETUP_OUTPUT is empty");
    }

    public Target PipelineTest => _ => _
        .Description("To be used by pipeline")
        .Executes(async () =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntries);

            await TestAppEntries(appEntries, splitArgs.Select(i => i.Key), GetPreSetupOutput());
        });

    public Target PipelineBuild => _ => _
        .Description("To be used by pipeline")
        .Executes(async () =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntries);

            await BuildAppEntries(appEntries, splitArgs.Select(i => i.Key), GetPreSetupOutput());
        });

    public Target PipelinePublish => _ => _
        .Description("To be used by pipeline")
        .Executes(async () =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntries);

            await PublishAppEntries(appEntries, splitArgs.Select(i => i.Key), GetPreSetupOutput());
        });

    public Target PipelinePreSetup => _ => _
        .Description("To be used by pipeline")
        .Executes(() =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntryConfigs);
            GetOrFail(() => GetAppEntries(), out var appEntries);
            GetOrFail(() => GetAppTestEntries(), out var appTestEntries);

            IReadOnlyCollection<Output>? lsRemote = null;

            List<(AppEntry AppEntry, string Env, SemVersion Version)> toRelease = new();

            foreach (var key in appEntryConfigs.Select(i => i.Key))
            {
                string appId = key;

                GetOrFail(appId, appEntryConfigs, out appId, out var appEntry);
                GetOrFail(() => GetAllVersions(appId, appEntryConfigs, ref lsRemote), out var allVersions);

                if (allVersions.GroupKeySorted.Count != 0)
                {
                    foreach (var groupKey in allVersions.GroupKeySorted)
                    {
                        string env;
                        if (string.IsNullOrEmpty(groupKey))
                        {
                            env = "main";
                            if (!Repository.Branch.Equals("master", StringComparison.OrdinalIgnoreCase) &&
                                !Repository.Branch.Equals("main", StringComparison.OrdinalIgnoreCase) &&
                                !Repository.Branch.Equals("prod", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                        }
                        else
                        {
                            env = groupKey;
                            if (!Repository.Branch.Equals(env, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                        }
                        if (!allVersions.LatestVersions.TryGetValue(groupKey, out SemVersion? value) || value != allVersions.VersionGrouped[groupKey].Last())
                        {
                            toRelease.Add((appEntry.Entry, env, allVersions.VersionGrouped[groupKey].Last()));
                            Log.Information("{appId} Tag: {current}, current latest: {latest}", appId, allVersions.VersionGrouped[groupKey].Last().ToString(), value);
                        }
                        else
                        {
                            Log.Information("{appId} Tag: {current}, already latest", appId, allVersions.VersionGrouped[groupKey].Last().ToString());
                        }
                    }
                }
            }

            foreach (var rel in toRelease)
            {
                Log.Information("{appId} on {env} has new version {newVersion}", rel.AppEntry.Id, rel.Env, rel.Version);
            }

            PreSetupOutput output = new()
            {
                HasRelease = toRelease.Count != 0,
                Releases = toRelease.ToDictionary(i => i.AppEntry.Id, i => new PreSetupOutputVersion()
                {
                    AppId = i.AppEntry.Id,
                    AppName = i.AppEntry.Name,
                    Environment = i.Env,
                    Version = i.Version.ToString()
                })
            };

            var serializedOutput = JsonSerializer.Serialize(output, _jsonSnakeCaseNamingOption);
            File.WriteAllText(RootDirectory / ".nuke" / "temp" / "pre_setup_output.json", serializedOutput);
            Log.Information("PRE_SETUP_OUTPUT: {output}", serializedOutput);

            switch (Args?.ToLowerInvariant())
            {
                case "github":
                    var outputTestMatrix = new List<PreSetupOutputMatrix>();
                    var outputBuildMatrix = new List<PreSetupOutputMatrix>();
                    var outputPublishMatrix = new List<PreSetupOutputMatrix>();
                    foreach (var appTestEntry in appTestEntries)
                    {
                        var appEntry = appEntryConfigs.First(i => i.Value.Tests.Any(j => j.Id == appTestEntry.Id)).Value.Entry;
                        var hasRelease = toRelease.Any(i => i.AppEntry.Id == appEntry.Id);
                        if (hasRelease || appTestEntry.RunType == TestRunType.Always)
                        {
                            PreSetupOutputMatrix preSetupOutputMatrix = new()
                            {
                                Id = appTestEntry.Id,
                                Name = appTestEntry.Name,
                                RunsOn = GetRunsOnGithub(appTestEntry.RunsOn),
                                BuildScript = GetBuildScriptGithub(appTestEntry.RunsOn),
                                IdsToRun = $"{appEntry.Id};{appTestEntry.Id}"
                            };
                            outputTestMatrix.Add(preSetupOutputMatrix);
                        }
                    }
                    foreach (var (Entry, Tests) in appEntryConfigs.Values)
                    {
                        var release = toRelease.FirstOrDefault(i => i.AppEntry.Id == Entry.Id);
                        if (release.AppEntry != null)
                        {
                            outputBuildMatrix.Add(new()
                            {
                                Id = Entry.Id,
                                Name = Entry.Name,
                                RunsOn = GetRunsOnGithub(Entry.BuildRunsOn),
                                BuildScript = GetBuildScriptGithub(Entry.BuildRunsOn),
                                IdsToRun = Entry.Id
                            });
                            outputPublishMatrix.Add(new()
                            {
                                Id = Entry.Id,
                                Name = Entry.Name,
                                RunsOn = GetRunsOnGithub(Entry.PublishRunsOn),
                                BuildScript = GetBuildScriptGithub(Entry.PublishRunsOn),
                                IdsToRun = Entry.Id
                            });
                        }
                    }
                    File.WriteAllText(RootDirectory / ".nuke" / "temp" / "pre_setup_output_test_matrix.json", JsonSerializer.Serialize(outputTestMatrix, _jsonSnakeCaseNamingOption));
                    File.WriteAllText(RootDirectory / ".nuke" / "temp" / "pre_setup_output_build_matrix.json", JsonSerializer.Serialize(outputBuildMatrix, _jsonSnakeCaseNamingOption));
                    File.WriteAllText(RootDirectory / ".nuke" / "temp" / "pre_setup_output_publish_matrix.json", JsonSerializer.Serialize(outputPublishMatrix, _jsonSnakeCaseNamingOption));
                    Log.Information("PRE_SETUP_OUTPUT_TEST_MATRIX: {outputMatrix}", JsonSerializer.Serialize(outputTestMatrix, _jsonSnakeCaseNamingOptionIndented));
                    Log.Information("PRE_SETUP_OUTPUT_BUILD_MATRIX: {outputMatrix}", JsonSerializer.Serialize(outputBuildMatrix, _jsonSnakeCaseNamingOptionIndented));
                    Log.Information("PRE_SETUP_OUTPUT_PUBLISH_MATRIX: {outputMatrix}", JsonSerializer.Serialize(outputPublishMatrix, _jsonSnakeCaseNamingOptionIndented));
                    break;
                default:
                    Log.Information("No agent pipeline provided");
                    break;
            }
        });
}
