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
            GetOrFail(() => GetAppEntries(), out var appEntries);
            GetOrFail(() => GetAppTestEntries(), out var appTestEntries);

            Func<string>? pipelineGetBranch = null;
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

            var branch = pipelineGetBranch();

            Log.Information("{appId} cccccccccccccccccc", branch);

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
                            if (!branch.Equals("master", StringComparison.OrdinalIgnoreCase) &&
                                !branch.Equals("main", StringComparison.OrdinalIgnoreCase) &&
                                !branch.Equals("prod", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                        }
                        else
                        {
                            env = groupKey;
                            if (!branch.Equals(env, StringComparison.OrdinalIgnoreCase))
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

            List<long> buildNumbers = new();

            string basePeel = "refs/tags/";
            foreach (var refs in lsRemote)
            {
                string rawTag = refs.Text[(refs.Text.IndexOf(basePeel) + basePeel.Length)..];

                if (rawTag.StartsWith("build.", StringComparison.OrdinalIgnoreCase))
                {
                    buildNumbers.Add(long.Parse(rawTag.Replace("build.", "")));
                }
            }

            var buildId = pipelineGetBuildId.Invoke();
            var buildMaxNumber = buildNumbers.Count != 0 ? buildNumbers.Max() : 0;

            PreSetupOutput output = new()
            {
                HasRelease = toRelease.Count != 0,
                IsFirstRelease = buildNumbers.Count == 0,
                BuildTag = $"build.{buildId}",
                LastBuildTag = $"build.{buildMaxNumber}",
                Releases = toRelease.ToDictionary(i => i.AppEntry.Id, i => new PreSetupOutputVersion()
                {
                    AppId = i.AppEntry.Id,
                    AppName = i.AppEntry.Name,
                    Environment = i.Env,
                    Version = i.Version.ToString()
                })
            };

            File.WriteAllText(RootDirectory / ".nuke" / "temp" / "pre_setup_output.json", JsonSerializer.Serialize(output, _jsonSnakeCaseNamingOption));
            Log.Information("PRE_SETUP_OUTPUT: {output}", JsonSerializer.Serialize(output, _jsonSnakeCaseNamingOptionIndented));

            File.WriteAllText(RootDirectory / ".nuke" / "temp" / "has_release.txt", toRelease.Count != 0 ? "true" : "false");
            File.WriteAllText(RootDirectory / ".nuke" / "temp" / "branch.txt", branch);

            pipelinePrepare?.Invoke(appTestEntries, appEntryConfigs, toRelease);
        });

    public Target PipelineRelease => _ => _
        .Description("To be used by pipeline")
        .Executes(() =>
        {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntryConfigs);

            var branchName = Environment.GetEnvironmentVariable("PRE_SETUP_HAS_RELEASE") ?? Repository.Branch;

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
            Git.Invoke($"tag -f {preSetupOutput.BuildTag}");
            Git.Invoke($"push -f --tags", logger: (s, e) => Log.Debug(e));

            string args = $"release create {preSetupOutput.BuildTag} {OutputPath / "*.zip"} " +
                $"--title {preSetupOutput.BuildTag} " +
                $"--target {branchName} " +
                $"--generate-notes";

            if (!preSetupOutput.IsFirstRelease)
            {
                args += $" --notes-start-tag {preSetupOutput.LastBuildTag}";
            }

            Gh.Invoke(args, logInvocation: false, logOutput: false);
        });
}
