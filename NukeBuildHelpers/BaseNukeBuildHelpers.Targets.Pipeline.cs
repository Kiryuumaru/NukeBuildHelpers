﻿using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.DependencyModel;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitReleaseManager;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Interfaces;
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
    public Target PipelineTest => _ => _
        .Unlisted()
        .Description("To be used by pipeline")
        .Executes(async () =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntries);

            await TestAppEntries(appEntries, splitArgs.Select(i => i.Key), GetPreSetupOutput());
        });

    public Target PipelineBuild => _ => _
        .Unlisted()
        .Description("To be used by pipeline")
        .Executes(async () =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntries);

            await BuildAppEntries(appEntries, splitArgs.Select(i => i.Key), GetPreSetupOutput());
        });

    public Target PipelinePublish => _ => _
        .Unlisted()
        .Description("To be used by pipeline")
        .Executes(async () =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntries);

            await PublishAppEntries(appEntries, splitArgs.Select(i => i.Key), GetPreSetupOutput());
        });

    public Target PipelinePreSetup => _ => _
        .Unlisted()
        .Description("To be used by pipeline")
        .Executes(() =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntryConfigs);
            GetOrFail(() => GetInstances<AppEntry>(), out var appEntries);
            GetOrFail(() => GetInstances<AppTestEntry>(), out var appTestEntries);

            IPipeline pipeline = (Args?.ToLowerInvariant()) switch
            {
                "github" => new GithubPipeline(this),
                "azure" => new AzurePipeline(this),
                _ => throw new Exception("No agent pipeline provided"),
            };

            var pipelineInfo = pipeline.GetPipelineInfo();

            Log.Information("Target branch: {branch}", pipelineInfo.Branch);
            Log.Information("Trigger type: {branch}", pipelineInfo.TriggerType);

            string groupKey;
            string environment;
            if (pipelineInfo.Branch.Equals(MainEnvironmentBranch, StringComparison.InvariantCultureIgnoreCase))
            {
                groupKey = "";
                environment = "main";
            }
            else
            {
                groupKey = pipelineInfo.Branch;
                environment = pipelineInfo.Branch;
            }

            IReadOnlyCollection<Output>? lsRemote = null;

            List<(AppEntry AppEntry, string Env, SemVersion Version)> toRelease = [];

            long targetBuildId = 0;
            long lastBuildId = 0;

            foreach (var key in appEntryConfigs.Select(i => i.Key))
            {
                string appId = key;

                GetOrFail(appId, appEntryConfigs, out appId, out var appEntry);
                GetOrFail(() => GetAllVersions(appId, appEntryConfigs, ref lsRemote), out var allVersions);

                if (allVersions.BuildIdCommitPaired.Count > 0)
                {
                    var maxBuildId = allVersions.BuildIdCommitPaired.Select(i => i.Key).Max();
                    lastBuildId = maxBuildId > lastBuildId ? maxBuildId : lastBuildId;
                }

                if (allVersions.EnvSorted.Count != 0)
                {
                    if (allVersions.EnvVersionGrouped.TryGetValue(groupKey, out var versionGroup) && versionGroup.Count > 0)
                    {
                        var lastVersionGroup = versionGroup.Last();
                        if (!allVersions.EnvLatestVersionPaired.TryGetValue(groupKey, out SemVersion? value) || value != lastVersionGroup)
                        {
                            allVersions.EnvLatestBuildIdPaired.TryGetValue(groupKey, out var allVersionLastId);
                            if (targetBuildId == 0)
                            {
                                targetBuildId = allVersionLastId;
                            }
                            else
                            {
                                targetBuildId = allVersionLastId < targetBuildId ? allVersionLastId : targetBuildId;
                            }
                            if (pipelineInfo.TriggerType == TriggerType.Tag)
                            {
                                toRelease.Add((appEntry.Entry, environment, lastVersionGroup));
                                Log.Information("{appId} Tag: {current}, current latest: {latest}", appId, lastVersionGroup.ToString(), value);
                            }
                        }
                        else
                        {
                            if (pipelineInfo.TriggerType == TriggerType.Tag)
                            {
                                Log.Information("{appId} Tag: {current}, already latest", appId, lastVersionGroup.ToString());
                            }
                        }
                    }
                }
            }

            foreach (var rel in toRelease)
            {
                Log.Information("{appId} on {env} has new version {newVersion}", rel.AppEntry.Id, rel.Env, rel.Version);
            }

            Dictionary<string, PreSetupOutputVersion> releases = toRelease.ToDictionary(i => i.AppEntry.Id, i => new PreSetupOutputVersion()
            {
                AppId = i.AppEntry.Id,
                AppName = i.AppEntry.Name,
                Environment = i.Env,
                Version = i.Version.ToString()
            });

            var releaseNotes = "";
            var buildId = lastBuildId + 1;
            var buildTag = "build." + buildId;
            var targetBuildTag = "build." + targetBuildId;
            var isFirstRelease = targetBuildId == 0;
            var hasRelease = toRelease.Count != 0;

            if (hasRelease)
            {
                Git.Invoke("tag -f " + buildTag);
                Git.Invoke("tag -f " + buildTag + "-" + environment);
                Git.Invoke("push -f --tags", logger: (s, e) => Log.Debug(e));

                string ghReleaseCreateArgs = $"release create {buildTag} " +
                    $"--title {buildTag} " +
                    $"--target {pipelineInfo.Branch} " +
                    $"--generate-notes " +
                    $"--draft";

                if (!isFirstRelease)
                {
                    ghReleaseCreateArgs += " --notes-start-tag " + targetBuildTag;
                }

                Gh.Invoke(ghReleaseCreateArgs, logInvocation: false, logOutput: false);

                var releaseNotesJson = Gh.Invoke($"release view {buildTag} --json body", logInvocation: false, logOutput: false).FirstOrDefault().Text;
                var releaseNotesJsonDocument = JsonSerializer.Deserialize<JsonDocument>(releaseNotesJson);
                if (releaseNotesJsonDocument == null ||
                    !releaseNotesJsonDocument.RootElement.TryGetProperty("body", out var releaseNotesProp) ||
                    releaseNotesProp.GetString() is not string releaseNotesFromProp)
                {
                    throw new Exception("releaseNotesJsonDocument is empty");
                }
                releaseNotes = releaseNotesFromProp;
            }
                
            PreSetupOutput output = new()
            {
                Branch = pipelineInfo.Branch,
                TriggerType = pipelineInfo.TriggerType,
                Environment = environment,
                HasRelease = hasRelease,
                ReleaseNotes = releaseNotes,
                IsFirstRelease = isFirstRelease,
                BuildId = buildId,
                LastBuildId = targetBuildId,
                Releases = releases,
            };

            File.WriteAllText(TemporaryDirectory / "pre_setup_output.json", JsonSerializer.Serialize(output, JsonExtension.SnakeCaseNamingOption));
            File.WriteAllText(TemporaryDirectory / "pre_setup_has_release.txt", hasRelease ? "true" : "false");

            Log.Information("NUKE_PRE_SETUP_OUTPUT: {output}", JsonSerializer.Serialize(output, JsonExtension.SnakeCaseNamingOptionIndented));

            pipeline.Prepare(output, appTestEntries, appEntryConfigs, toRelease);
        });

    public Target PipelinePostSetup => _ => _
        .Unlisted()
        .Description("To be used by pipeline")
        .Executes(() =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntryConfigs);

            var preSetupOutput = GetPreSetupOutput();

            if (preSetupOutput.HasRelease)
            {
                if (Environment.GetEnvironmentVariable("NUKE_PUBLISH_SUCCESS") == "ok")
                {
                    foreach (var release in OutputDirectory.GetDirectories())
                    {
                        if (!preSetupOutput.Releases.TryGetValue(release.Name, out var preSetupOutputVersion))
                        {
                            continue;
                        }
                        var outPath = OutputDirectory / release.Name + "-" + preSetupOutputVersion.Version;
                        var outPathZip = OutputDirectory / release.Name + "-" + preSetupOutputVersion.Version + ".zip";
                        release.CopyFilesRecursively(outPath);
                        outPath.ZipTo(outPathZip);
                    }
                    foreach (var release in OutputDirectory.GetFiles())
                    {
                        Log.Information("Publish: {name}", release.Name);
                    }

                    foreach (var release in preSetupOutput.Releases.Values)
                    {
                        if (!appEntryConfigs.TryGetValue(release.AppId, out var appEntry))
                        {
                            continue;
                        }
                        string latestTag = "latest";
                        if (!release.Environment.Equals("main", StringComparison.InvariantCultureIgnoreCase))
                        {
                            latestTag += "-" + release.Environment.ToLowerInvariant();
                        }
                        if (appEntry.Entry.MainRelease)
                        {
                            Git.Invoke("tag -f " + release.Version);
                            Git.Invoke("tag -f " + latestTag);
                        }
                        else
                        {
                            Git.Invoke("tag -f " + appEntry.Entry.Id.ToLowerInvariant() + "/" + release.Version);
                            Git.Invoke("tag -f " + appEntry.Entry.Id.ToLowerInvariant() + "/" + latestTag);
                        }
                    }

                    Git.Invoke("tag -f build." + preSetupOutput.BuildId + "-" + preSetupOutput.Environment + "-passed", logger: (s, e) => Log.Debug(e));

                    Git.Invoke("push -f --tags", logger: (s, e) => Log.Debug(e));

                    Gh.Invoke("release upload --clobber build." + preSetupOutput.BuildId + " " + string.Join(" ", OutputDirectory.GetFiles("*.zip").Select(i => i.ToString())));

                    Gh.Invoke("release edit --draft=false build." + preSetupOutput.BuildId);
                }
                else
                {
                    Gh.Invoke("release delete -y build." + preSetupOutput.BuildId);

                    Git.Invoke("tag -f build." + preSetupOutput.BuildId + "-" + preSetupOutput.Environment + "-failed", logger: (s, e) => Log.Debug(e));

                    Git.Invoke("push -f --tags", logger: (s, e) => Log.Debug(e));
                }
            }
        });

    private static PreSetupOutput GetPreSetupOutput()
    {
        string? preSetupOutputValue = Environment.GetEnvironmentVariable("NUKE_PRE_SETUP_OUTPUT");

        if (string.IsNullOrEmpty(preSetupOutputValue))
        {
            throw new Exception("NUKE_PRE_SETUP_OUTPUT is empty");
        }

        PreSetupOutput? preSetupOutput = JsonSerializer.Deserialize<PreSetupOutput>(preSetupOutputValue, JsonExtension.SnakeCaseNamingOption);

        return preSetupOutput ?? throw new Exception("NUKE_PRE_SETUP_OUTPUT is empty");
    }
}
