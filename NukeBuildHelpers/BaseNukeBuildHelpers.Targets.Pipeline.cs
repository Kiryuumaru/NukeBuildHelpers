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

            switch (Args?.ToLowerInvariant())
            {
                case "github":
                    GithubPipelinePrepare(appTestEntries, appEntryConfigs, toRelease);
                    break;
                default:
                    Log.Information("No agent pipeline provided");
                    break;
            }
        });
}
