using ICSharpCode.SharpZipLib.Zip;
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
            GetOrFail(() => GetEntries<AppEntry>(), out var appEntries);
            GetOrFail(() => GetEntries<AppTestEntry>(), out var appTestEntries);

            Func<PipelineInfo>? pipelineGetBranch = null;
            Func<long>? pipelineGetBuildId = null;
            Action<List<AppTestEntry>, Dictionary<string, (AppEntry Entry, List<AppTestEntry> Tests)>, List<(AppEntry AppEntry, string Env, SemVersion Version)>>? pipelinePrepare = null;

            switch (Args?.ToLowerInvariant())
            {
                case "github":
                    pipelineGetBranch = () => GithubPipelineGetBranch();
                    pipelineGetBuildId = () => GithubPipelineGetBuildId();
                    pipelinePrepare = (appTestEntries, appEntryConfigs, toRelease) => GithubPipelinePrepare(appTestEntries, appEntryConfigs, toRelease);
                    break;
                default:
                    throw new Exception("No agent pipeline provided");
            }

            var pipelineInfo = pipelineGetBranch();

            Log.Information("Target branch: {branch}", pipelineInfo.Branch);
            Log.Information("Trigger type: {branch}", pipelineInfo.TriggerType);

            IReadOnlyCollection<Output>? lsRemote = null;

            List<(AppEntry AppEntry, string Env, SemVersion Version)> toRelease = [];

            foreach (var key in appEntryConfigs.Select(i => i.Key))
            {
                string appId = key;

                GetOrFail(appId, appEntryConfigs, out appId, out var appEntry);
                GetOrFail(() => GetAllVersions(appId, appEntryConfigs, ref lsRemote), out var allVersions);

                if (allVersions.GroupKeySorted.Count != 0 && pipelineInfo.TriggerType == TriggerType.Tag)
                {
                    foreach (var groupKey in allVersions.GroupKeySorted)
                    {
                        string env;
                        if (string.IsNullOrEmpty(groupKey))
                        {
                            env = "main";
                            if (!pipelineInfo.Branch.Equals("master", StringComparison.OrdinalIgnoreCase) &&
                                !pipelineInfo.Branch.Equals("main", StringComparison.OrdinalIgnoreCase) &&
                                !pipelineInfo.Branch.Equals("prod", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                        }
                        else
                        {
                            env = groupKey;
                            if (!pipelineInfo.Branch.Equals(env, StringComparison.OrdinalIgnoreCase))
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

            if (lsRemote == null)
            {
                throw new Exception("lsRemote is null");
            }

            foreach (var rel in toRelease)
            {
                Log.Information("{appId} on {env} has new version {newVersion}", rel.AppEntry.Id, rel.Env, rel.Version);
            }

            List<long> buildNumbers = [];

            string basePeel = "refs/tags/";
            foreach (var refs in lsRemote)
            {
                string rawTag = refs.Text[(refs.Text.IndexOf(basePeel) + basePeel.Length)..];

                if (rawTag.StartsWith("build.", StringComparison.OrdinalIgnoreCase))
                {
                    buildNumbers.Add(long.Parse(rawTag.Replace("build.", "")));
                }
            }

            long buildMaxNumber = 0;

            foreach (var buildNumber in buildNumbers.OrderByDescending(i => i))
            {
                bool hasMatched = false;
                foreach (var line in Git.Invoke($"branch -r --contains refs/tags/build.{buildNumber}"))
                {
                    var containBranch = line.Text;
                    containBranch = containBranch[(containBranch.IndexOf('/') + 1)..];
                    if (containBranch.Equals(pipelineInfo.Branch, StringComparison.OrdinalIgnoreCase))
                    {
                        buildMaxNumber = buildNumber;
                        hasMatched = true;
                        break;
                    }
                }
                if (hasMatched)
                {
                    break;
                }
            }

            Dictionary<string, PreSetupOutputVersion> releases = toRelease.ToDictionary(i => i.AppEntry.Id, i => new PreSetupOutputVersion()
            {
                AppId = i.AppEntry.Id,
                AppName = i.AppEntry.Name,
                Environment = i.Env,
                Version = i.Version.ToString()
            });

            var releaseNotes = "";
            var buildId = pipelineGetBuildId.Invoke();
            var buildTag = $"build.{buildId}";
            var lastBuildTag = $"build.{buildMaxNumber}";
            var isFirstRelease = buildMaxNumber == 0;
            var hasRelease = toRelease.Count != 0;

            if (hasRelease)
            {
                Git.Invoke($"tag -f {buildTag}");
                Git.Invoke($"push -f --tags", logger: (s, e) => Log.Debug(e));

                string ghReleaseCreateArgs = $"release create {buildTag} " +
                    $"--title {buildTag} " +
                    $"--target {pipelineInfo.Branch} " +
                    $"--generate-notes " +
                    $"--draft";

                if (!isFirstRelease)
                {
                    ghReleaseCreateArgs += $" --notes-start-tag {lastBuildTag}";
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
                HasRelease = hasRelease,
                ReleaseNotes = releaseNotes,
                IsFirstRelease = isFirstRelease,
                BuildTag = buildTag,
                LastBuildTag = lastBuildTag,
                Releases = releases
            };

            File.WriteAllText(TempPath / "pre_setup_output.json", JsonSerializer.Serialize(output, _jsonSnakeCaseNamingOption));
            File.WriteAllText(TempPath / "pre_setup_has_release.txt", hasRelease ? "true" : "false");

            Log.Information("PRE_SETUP_OUTPUT: {output}", JsonSerializer.Serialize(output, _jsonSnakeCaseNamingOptionIndented));

            pipelinePrepare?.Invoke(appTestEntries, appEntryConfigs, toRelease);
        });

    public Target PipelineRelease => _ => _
        .Description("To be used by pipeline")
        .Executes(() =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntryConfigs);

            var preSetupOutput = GetPreSetupOutput();

            foreach (var release in OutputPath.GetDirectories())
            {
                if (!preSetupOutput.Releases.TryGetValue(release.Name, out var preSetupOutputVersion))
                {
                    continue;
                }
                var outPath = OutputPath / $"{release.Name}-{preSetupOutputVersion.Version}";
                var outPathZip = OutputPath / $"{release.Name}-{preSetupOutputVersion.Version}.zip";
                release.CopyFilesRecursively(outPath);
                outPath.ZipTo(outPathZip);
            }
            foreach (var release in OutputPath.GetFiles())
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
                if (!release.Environment.Equals("main", StringComparison.OrdinalIgnoreCase))
                {
                    latestTag += "-" + release.Environment.ToLowerInvariant();
                }
                Git.Invoke($"tag -f {release.Version}");
                if (appEntry.Entry.MainRelease)
                {
                    Git.Invoke($"tag -f {latestTag}");
                }
                else
                {
                    Git.Invoke($"tag -f {appEntry.Entry.Id.ToLowerInvariant()}/{latestTag}");
                }
            }
            Git.Invoke($"push -f --tags", logger: (s, e) => Log.Debug(e));

            Gh.Invoke($"release upload {preSetupOutput.BuildTag} {string.Join(" ", OutputPath.GetFiles("*.zip").Select(i => i.ToString()))}");

            Gh.Invoke($"release edit {preSetupOutput.BuildTag} --draft=false");
        });
}
