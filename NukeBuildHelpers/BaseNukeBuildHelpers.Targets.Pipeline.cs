using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Utilities.Collections;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Models;
using NukeBuildHelpers.Pipelines.Interfaces;
using Semver;
using Serilog;
using System.Text.Json;

namespace NukeBuildHelpers;

partial class BaseNukeBuildHelpers
{
    public Target PipelineTest => _ => _
        .Unlisted()
        .Description("To be used by pipeline")
        .Executes(async () =>
        {
            ValueHelpers.GetOrFail(() => SplitArgs, out var splitArgs);
            ValueHelpers.GetOrFail(() => GetAppConfig(), out var appConfig);

            await TestAppEntries(appConfig, splitArgs.Select(i => i.Key), GetPreSetupOutput());
        });

    public Target PipelineBuild => _ => _
        .Unlisted()
        .Description("To be used by pipeline")
        .Executes(async () =>
        {
            ValueHelpers.GetOrFail(() => SplitArgs, out var splitArgs);
            ValueHelpers.GetOrFail(() => GetAppConfig(), out var appConfig);

            await BuildAppEntries(appConfig, splitArgs.Select(i => i.Key), GetPreSetupOutput());
        });

    public Target PipelinePublish => _ => _
        .Unlisted()
        .Description("To be used by pipeline")
        .Executes(async () =>
        {
            ValueHelpers.GetOrFail(() => SplitArgs, out var splitArgs);
            ValueHelpers.GetOrFail(() => GetAppConfig(), out var appConfig);

            await PublishAppEntries(appConfig, splitArgs.Select(i => i.Key), GetPreSetupOutput());
        });

    public Target PipelinePreSetup => _ => _
        .Unlisted()
        .Description("To be used by pipeline")
        .Executes(() =>
        {
            ValueHelpers.GetOrFail(() => SplitArgs, out var splitArgs);
            ValueHelpers.GetOrFail(() => GetAppConfig(), out var appConfig);

            IPipeline pipeline = (Args?.ToLowerInvariant()) switch
            {
                "github" => new GithubPipeline(this),
                "azure" => new AzurePipeline(this),
                _ => throw new Exception("No agent pipeline provided"),
            };

            var pipelineInfo = pipeline.GetPipelineInfo();

            Log.Information("Target branch: {branch}", pipelineInfo.Branch);
            Log.Information("Trigger type: {branch}", pipelineInfo.TriggerType);

            string env = pipelineInfo.Branch.ToLowerInvariant();

            IReadOnlyCollection<Output>? lsRemote = null;

            Dictionary<string, AppRunEntry> toEntry = [];

            long targetBuildId = 0;
            long lastBuildId = 0;

            foreach (var key in appConfig.AppEntryConfigs.Select(i => i.Key))
            {
                string appId = key;

                ValueHelpers.GetOrFail(appId, appConfig.AppEntryConfigs, out appId, out var appEntry);
                ValueHelpers.GetOrFail(() => GetAllVersions(appId, appConfig.AppEntryConfigs, ref lsRemote), out var allVersions);

                if (allVersions.BuildIdCommitPaired.Count > 0)
                {
                    var maxBuildId = allVersions.BuildIdCommitPaired.Select(i => i.Key).Max();
                    lastBuildId = Math.Max(maxBuildId, lastBuildId);
                }

                if (allVersions.EnvSorted.Count != 0 &&
                    allVersions.EnvVersionGrouped.TryGetValue(env, out var versionGroup) && versionGroup.Count > 0)
                {
                    var lastVersionGroup = versionGroup.Last();

                    bool hasBumped = false;

                    if (!allVersions.EnvLatestVersionPaired.TryGetValue(env, out var value) || value != lastVersionGroup)
                    {
                        if (allVersions.VersionBump.Contains(lastVersionGroup) &&
                            !allVersions.VersionQueue.Contains(lastVersionGroup) &&
                            !allVersions.VersionFailed.Contains(lastVersionGroup) &&
                            !allVersions.VersionPassed.Contains(lastVersionGroup))
                        {
                            allVersions.EnvLatestBuildIdPaired.TryGetValue(env, out var allVersionLastId);
                            targetBuildId = targetBuildId == 0 ? allVersionLastId : Math.Min(allVersionLastId, targetBuildId);
                            if (pipelineInfo.TriggerType == TriggerType.Tag)
                            {
                                hasBumped = true;
                                Log.Information("{appId} Tag: {current}, current latest: {latest}", appId, lastVersionGroup.ToString());
                            }
                        }
                    }
                    else
                    {
                        if (pipelineInfo.TriggerType == TriggerType.Tag)
                        {
                            Log.Information("{appId} Tag: {current}, already latest", appId, lastVersionGroup.ToString());
                        }
                    }

                    toEntry.Add(appId, new()
                    {
                        AppEntry = appEntry.Entry,
                        Env = env,
                        Version = lastVersionGroup,
                        HasRelease = hasBumped
                    });
                }
            }

            foreach (var rel in toEntry.Values.Where(i => i.HasRelease))
            {
                Log.Information("{appId} on {env} has new version {newVersion}", rel.AppEntry.Id, rel.Env, rel.Version);
            }

            var releaseNotes = "";
            var buildId = lastBuildId + 1;
            var buildTag = "build." + buildId;
            var targetBuildTag = "build." + targetBuildId;
            var isFirstRelease = targetBuildId == 0;
            var hasEntries = toEntry.Count != 0;
            var hasRelease = toEntry.Any(i => i.Value.HasRelease);

            string versionFactory(SemVersion version)
            {
                if (pipelineInfo.TriggerType == TriggerType.PullRequest)
                {
                    return $"{version}+build.{buildId}-pr.{pipelineInfo.PullRequestNumber}";
                }
                else
                {
                    return $"{version}+build.{buildId}";
                }
            }

            Dictionary<string, PreSetupOutputVersion> entries = toEntry.Values.ToDictionary(i => i.AppEntry.Id, i => new PreSetupOutputVersion()
            {
                AppId = i.AppEntry.Id,
                AppName = i.AppEntry.Name,
                Environment = i.Env,
                Version = versionFactory(i.Version),
                HasRelease = i.HasRelease
            });

            if (hasRelease)
            {
                foreach (var entry in toEntry.Values.Where(i => i.HasRelease))
                {
                    var version = entry.Version.WithoutMetadata();
                    if (entry.AppEntry.MainRelease)
                    {
                        Git.Invoke("tag -f " + version + "-queue");
                        Git.Invoke("tag -f " + version);
                    }
                    else
                    {
                        Git.Invoke("tag -f " + entry.AppEntry.Id.ToLowerInvariant() + "/" + version + "-queue");
                        Git.Invoke("tag -f " + entry.AppEntry.Id.ToLowerInvariant() + "/" + version);
                    }
                }

                Git.Invoke("tag -f " + buildTag, logger: (s, e) => Log.Debug(e));
                Git.Invoke("tag -f " + buildTag + "-" + env, logger: (s, e) => Log.Debug(e));

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

                Gh.Invoke(ghReleaseCreateArgs, logger: (s, e) => Log.Debug(e));

                var releaseNotesJson = Gh.Invoke($"release view {buildTag} --json body", logger: (s, e) => Log.Debug(e)).FirstOrDefault().Text;
                var releaseNotesJsonDocument = JsonSerializer.Deserialize<JsonDocument>(releaseNotesJson);
                if (releaseNotesJsonDocument == null ||
                    !releaseNotesJsonDocument.RootElement.TryGetProperty("body", out var releaseNotesProp) ||
                    releaseNotesProp.GetString() is not string releaseNotesFromProp)
                {
                    throw new Exception("releaseNotesJsonDocument is empty");
                }
                releaseNotes = releaseNotesFromProp;
            }

            List<string> toTest = [];
            List<string> toBuild = [];
            List<string> toPublish = [];

            bool hasTest = false;
            bool hasBuild = false;
            bool hasPublish = false;

            foreach (var appEntryConfig in appConfig.AppEntryConfigs.Values)
            {
                if (!toEntry.TryGetValue(appEntryConfig.Entry.Id, out var entry))
                {
                    continue;
                }
                if ((pipelineInfo.TriggerType == TriggerType.PullRequest && appEntryConfig.Entry.RunBuildOn.HasFlag(RunType.PullRequest)) ||
                    (pipelineInfo.TriggerType == TriggerType.Commit && appEntryConfig.Entry.RunBuildOn.HasFlag(RunType.Commit)) ||
                    (pipelineInfo.TriggerType == TriggerType.Tag && appEntryConfig.Entry.RunBuildOn.HasFlag(RunType.Bump) && entry.HasRelease))
                {
                    toBuild.Add(appEntryConfig.Entry.Id);
                    hasBuild = true;
                }
                if ((pipelineInfo.TriggerType == TriggerType.PullRequest && appEntryConfig.Entry.RunPublishOn.HasFlag(RunType.PullRequest)) ||
                    (pipelineInfo.TriggerType == TriggerType.Commit && appEntryConfig.Entry.RunPublishOn.HasFlag(RunType.Commit)) ||
                    (pipelineInfo.TriggerType == TriggerType.Tag && appEntryConfig.Entry.RunPublishOn.HasFlag(RunType.Bump) && entry.HasRelease))
                {
                    toPublish.Add(appEntryConfig.Entry.Id);
                    hasPublish = true;
                }
            }

            foreach (var appTestEntry in appConfig.AppTestEntries.Values)
            {
                var appEntry = appConfig.AppEntries.Values.FirstOrDefault(i => appTestEntry.AppEntryTargets.Contains(i.GetType()));
                if (appEntry == null)
                {
                    continue;
                }
                if (appTestEntry.RunTestOn == RunTestType.All ||
                    ((toBuild.Any(i => i == appEntry.Id) || toBuild.Any(i => i == appEntry.Id)) && appTestEntry.RunTestOn.HasFlag(RunTestType.Target)))
                {
                    toTest.Add(appTestEntry.Id);
                    hasTest = true;
                }
            }

            PreSetupOutput output = new()
            {
                Branch = pipelineInfo.Branch,
                TriggerType = pipelineInfo.TriggerType,
                Environment = env,
                HasRelease = hasRelease,
                HasEntries = hasEntries,
                HasTest = hasTest,
                HasBuild = hasBuild,
                HasPublish = hasPublish,
                ReleaseNotes = releaseNotes,
                IsFirstRelease = isFirstRelease,
                BuildId = buildId,
                LastBuildId = targetBuildId,
                Entries = entries,
                ToTest = toTest,
                ToBuild = toBuild,
                ToPublish = toPublish,
            };

            File.WriteAllText(TemporaryDirectory / "pre_setup_output.json", JsonSerializer.Serialize(output, JsonExtension.SnakeCaseNamingOption));
            File.WriteAllText(TemporaryDirectory / "pre_setup_has_release.txt", hasRelease ? "true" : "false");
            File.WriteAllText(TemporaryDirectory / "pre_setup_has_entries.txt", hasEntries ? "true" : "false");
            File.WriteAllText(TemporaryDirectory / "pre_setup_has_test.txt", hasTest ? "true" : "false");
            File.WriteAllText(TemporaryDirectory / "pre_setup_has_build.txt", hasBuild ? "true" : "false");
            File.WriteAllText(TemporaryDirectory / "pre_setup_has_publish.txt", hasPublish ? "true" : "false");

            Log.Information("NUKE_PRE_SETUP_OUTPUT: {output}", JsonSerializer.Serialize(output, JsonExtension.SnakeCaseNamingOptionIndented));

            pipeline.Prepare(output, appConfig, toEntry);
        });

    public Target PipelinePostSetup => _ => _
        .Unlisted()
        .Description("To be used by pipeline")
        .Executes(() =>
        {
            ValueHelpers.GetOrFail(() => SplitArgs, out var splitArgs);
            ValueHelpers.GetOrFail(() => GetAppConfig(), out var appConfig);

            var preSetupOutput = GetPreSetupOutput();

            if (preSetupOutput.HasRelease)
            {
                if (Environment.GetEnvironmentVariable("NUKE_PUBLISH_SUCCESS") == "ok")
                {
                    foreach (var release in OutputDirectory.GetDirectories())
                    {
                        if (!preSetupOutput.Entries.TryGetValue(release.Name, out var preSetupOutputVersion))
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

                    foreach (var release in preSetupOutput.Entries.Values)
                    {
                        if (!appConfig.AppEntryConfigs.TryGetValue(release.AppId, out var appEntry))
                        {
                            continue;
                        }
                        var version = SemVersion.Parse(release.Version, SemVersionStyles.Strict).WithoutMetadata();
                        string latestTag = "latest";
                        if (!release.Environment.Equals(MainEnvironmentBranch, StringComparison.InvariantCultureIgnoreCase))
                        {
                            latestTag += "-" + release.Environment.ToLowerInvariant();
                        }
                        if (appEntry.Entry.MainRelease)
                        {
                            Git.Invoke("tag -f " + version + "-passed");
                            Git.Invoke("tag -f " + version);
                            Git.Invoke("tag -f " + latestTag);
                        }
                        else
                        {
                            Git.Invoke("tag -f " + appEntry.Entry.Id.ToLowerInvariant() + "/" + version + "-passed");
                            Git.Invoke("tag -f " + appEntry.Entry.Id.ToLowerInvariant() + "/" + version);
                            Git.Invoke("tag -f " + appEntry.Entry.Id.ToLowerInvariant() + "/" + latestTag);
                        }
                    }

                    Git.Invoke("push -f --tags", logger: (s, e) => Log.Debug(e));

                    Gh.Invoke("release upload --clobber build." + preSetupOutput.BuildId + " " + string.Join(" ", OutputDirectory.GetFiles("*.zip").Select(i => i.ToString())));

                    Gh.Invoke("release edit --draft=false build." + preSetupOutput.BuildId);
                }
                else
                {
                    foreach (var release in preSetupOutput.Entries.Values)
                    {
                        if (!appConfig.AppEntryConfigs.TryGetValue(release.AppId, out var appEntry))
                        {
                            continue;
                        }
                        var version = SemVersion.Parse(release.Version, SemVersionStyles.Strict).WithoutMetadata();
                        string latestTag = "latest";
                        if (!release.Environment.Equals(MainEnvironmentBranch, StringComparison.InvariantCultureIgnoreCase))
                        {
                            latestTag += "-" + release.Environment.ToLowerInvariant();
                        }
                        if (appEntry.Entry.MainRelease)
                        {
                            Git.Invoke("tag -f " + version + "-failed");
                            Git.Invoke("tag -f " + version);
                        }
                        else
                        {
                            Git.Invoke("tag -f " + appEntry.Entry.Id.ToLowerInvariant() + "/" + version + "-failed");
                            Git.Invoke("tag -f " + appEntry.Entry.Id.ToLowerInvariant() + "/" + version);
                        }
                    }

                    Gh.Invoke("release delete -y build." + preSetupOutput.BuildId);

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
