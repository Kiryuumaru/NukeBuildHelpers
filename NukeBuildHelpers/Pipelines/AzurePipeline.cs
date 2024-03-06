using ICSharpCode.SharpZipLib.Zip;
using Nuke.Common;
using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using Nuke.Utilities.Text.Yaml;
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
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace NukeBuildHelpers;

internal class AzurePipeline(BaseNukeBuildHelpers nukeBuild) : IPipeline
{
    public BaseNukeBuildHelpers NukeBuild { get; set; } = nukeBuild;

    public long GetBuildId()
    {
        return AzurePipelines.Instance.BuildId;
    }

    public PipelineInfo GetPipelineInfo()
    {
        TriggerType triggerType = TriggerType.Commit;
        var branch = Environment.GetEnvironmentVariable("BUILD_SOURCEBRANCHNAME");

        if (string.IsNullOrEmpty(branch))
        {
            branch = NukeBuild.Repository.Branch;
        }
        else
        {
            if (Environment.GetEnvironmentVariable("BUILD_REASON") == "PullRequest")
            {
                var targetBranch = Environment.GetEnvironmentVariable("SYSTEM_PULLREQUEST_TARGETBRANCHNAME");

                while (true)
                {
                    var result = NukeBuild.Gh.Invoke($"pr view {targetBranch} --json baseRefName --jq .baseRefName").FirstOrDefault().Text;

                    if (result == null)
                    {
                        break;
                    }
                    else
                    {
                        targetBranch = result;
                        branch = targetBranch;
                    }
                }
            }
        }
        return new()
        {
            Branch = branch,
            TriggerType = triggerType,
        };
    }

    public void Prepare(List<AppTestEntry> appTestEntries, Dictionary<string, (AppEntry Entry, List<AppTestEntry> Tests)> appEntryConfigs, List<(AppEntry AppEntry, string Env, SemVersion Version)> toRelease)
    {
        var outputTestMatrix = new List<PreSetupOutputAppTestEntryMatrix>();
        var outputBuildMatrix = new List<PreSetupOutputAppEntryMatrix>();
        var outputPublishMatrix = new List<PreSetupOutputAppEntryMatrix>();
        foreach (var appTestEntry in appTestEntries)
        {
            var appEntry = appEntryConfigs.First(i => i.Value.Tests.Any(j => j.Id == appTestEntry.Id)).Value.Entry;
            var hasRelease = toRelease.Any(i => i.AppEntry.Id == appEntry.Id);
            if (hasRelease || appTestEntry.RunType == TestRunType.Always)
            {
                PreSetupOutputAppTestEntryMatrix preSetupOutputMatrix = new()
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
        if (outputTestMatrix.Count == 0 && appTestEntries.Count != 0)
        {
            PreSetupOutputAppTestEntryMatrix preSetupOutputMatrix = new()
            {
                Id = "skip",
                Name = "Skip",
                RunsOn = GetRunsOnGithub(RunsOnType.Ubuntu2204),
                BuildScript = "",
                IdsToRun = ""
            };
            outputTestMatrix.Add(preSetupOutputMatrix);
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
                    IdsToRun = Entry.Id,
                    Version = release.Version.ToString() + "+build." + GitHubActions.Instance.RunId,
                });
                outputPublishMatrix.Add(new()
                {
                    Id = Entry.Id,
                    Name = Entry.Name,
                    RunsOn = GetRunsOnGithub(Entry.PublishRunsOn),
                    BuildScript = GetBuildScriptGithub(Entry.PublishRunsOn),
                    IdsToRun = Entry.Id,
                    Version = release.Version.ToString() + "+build." + GitHubActions.Instance.RunId,
                });
            }
        }
        File.WriteAllText(BaseNukeBuildHelpers.TempPath / "pre_setup_output_test_matrix.json", JsonSerializer.Serialize(outputTestMatrix, JsonExtension.SnakeCaseNamingOption));
        File.WriteAllText(BaseNukeBuildHelpers.TempPath / "pre_setup_output_build_matrix.json", JsonSerializer.Serialize(outputBuildMatrix, JsonExtension.SnakeCaseNamingOption));
        File.WriteAllText(BaseNukeBuildHelpers.TempPath / "pre_setup_output_publish_matrix.json", JsonSerializer.Serialize(outputPublishMatrix, JsonExtension.SnakeCaseNamingOption));
        Log.Information("PRE_SETUP_OUTPUT_TEST_MATRIX: {outputMatrix}", JsonSerializer.Serialize(outputTestMatrix, JsonExtension.SnakeCaseNamingOptionIndented));
        Log.Information("PRE_SETUP_OUTPUT_BUILD_MATRIX: {outputMatrix}", JsonSerializer.Serialize(outputBuildMatrix, JsonExtension.SnakeCaseNamingOptionIndented));
        Log.Information("PRE_SETUP_OUTPUT_PUBLISH_MATRIX: {outputMatrix}", JsonSerializer.Serialize(outputPublishMatrix, JsonExtension.SnakeCaseNamingOptionIndented));
    }

    public void BuildWorkflow()
    {

    }

    private static string GetRunsOnGithub(RunsOnType runsOnType)
    {
        return runsOnType switch
        {
            RunsOnType.WindowsLatest => "windows-latest",
            RunsOnType.Windows2022 => "windows-2022",
            RunsOnType.UbuntuLatest => "ubuntu-latest",
            RunsOnType.Ubuntu2204 => "ubuntu-22.04",
            _ => throw new NotImplementedException()
        };
    }

    private static string GetBuildScriptGithub(RunsOnType runsOnType)
    {
        return runsOnType switch
        {
            RunsOnType.WindowsLatest => "./build.cmd",
            RunsOnType.Windows2022 => "./build.cmd",
            RunsOnType.UbuntuLatest => "chmod +x ./build.sh && ./build.sh",
            RunsOnType.Ubuntu2204 => "chmod +x ./build.sh && ./build.sh",
            _ => throw new NotImplementedException()
        };
    }
}
