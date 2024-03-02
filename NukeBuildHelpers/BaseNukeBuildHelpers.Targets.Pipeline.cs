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
        string preSetupOutputValue = Environment.GetEnvironmentVariable("PRE_SETUP_OUTPUT");

        if (string.IsNullOrEmpty(preSetupOutputValue))
        {
            throw new Exception("PRE_SETUP_OUTPUT is empty");
        }

        PreSetupOutput preSetupOutput = JsonSerializer.Deserialize<PreSetupOutput>(preSetupOutputValue);

        if (preSetupOutput == null)
        {
            throw new Exception("PRE_SETUP_OUTPUT is empty");
        }

        return preSetupOutput;
    }

    public Target PipelinePrepare => _ => _
        .Description("To be used by pipeline")
        .Executes(async () =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntries);

            await PrepareAppEntries(appEntries, splitArgs.Select(i => i.Key));
        });

    public Target PipelineTest => _ => _
        .Description("To be used by pipeline")
        .Executes(async () =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntries);

            await TestAppEntries(appEntries, splitArgs.Select(i => i.Key));
        });

    public Target PipelineBuild => _ => _
        .Description("To be used by pipeline")
        .Executes(async () =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntries);

            PreSetupOutput preSetupOutput = GetPreSetupOutput();

            Log.Information("From out: {ss}", JsonSerializer.Serialize(preSetupOutput, new JsonSerializerOptions() { WriteIndented = true }));

            await BuildAppEntries(appEntries, splitArgs.Select(i => i.Key));
        });

    public Target PipelinePack => _ => _
        .Description("To be used by pipeline")
        .Executes(async () =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntries);

            await PackAppEntries(appEntries, splitArgs.Select(i => i.Key));
        });

    public Target PipelinePublish => _ => _
        .Description("To be used by pipeline")
        .Executes(async () =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntries);

            await PublishAppEntries(appEntries, splitArgs.Select(i => i.Key));
        });

    public Target PipelinePreSetup => _ => _
        .Description("To be used by pipeline")
        .DependsOn(Version)
        .Executes(() =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntryConfigs);

            IReadOnlyCollection<Output> lsRemote = null;

            List<(string AppId, string Env, SemVersion Version)> toRelease = new();

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
                        if (!allVersions.LatestVersions.ContainsKey(groupKey) || allVersions.LatestVersions[groupKey] != allVersions.VersionGrouped[groupKey].Last())
                        {
                            toRelease.Add((appId, env, allVersions.VersionGrouped[groupKey].Last()));
                            Log.Information("{appId} Tag: {current}, current latest: {latest}", appId, allVersions.VersionGrouped[groupKey].Last().ToString(), allVersions.LatestVersions.ContainsKey(groupKey) ? allVersions.LatestVersions[groupKey].ToString() : "non");
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
                Log.Information("{appId} on {env} has new version {newVersion}", rel.AppId, rel.Env, rel.Version);
            }

            PreSetupOutput output = new()
            {
                HasRelease = toRelease.Any(),
                Releases = toRelease.ToDictionary(i => i.AppId, i => new PreSetupOutputVersion()
                {
                    AppId = i.AppId,
                    Environment = i.Env,
                    Version = i.Version 
                })
            };

            var serializedOutput = JsonSerializer.Serialize(output);

            Log.Information("PRE_SETUP_OUTPUT: {output}", serializedOutput);

            File.WriteAllText(RootDirectory / ".nuke" / "temp" / "pre_setup_output.json", serializedOutput);
        });
}
