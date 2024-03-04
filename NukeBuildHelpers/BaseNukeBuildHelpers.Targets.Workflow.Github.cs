using ICSharpCode.SharpZipLib.Zip;
using Nuke.Common;
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

partial class BaseNukeBuildHelpers
{
    private readonly Serializer _yamlSerializer = new();

    private static void GithubPipelinePrepare(List<AppTestEntry> appTestEntries, Dictionary<string, (AppEntry Entry, List<AppTestEntry> Tests)> appEntryConfigs, List<(AppEntry AppEntry, string Env, SemVersion Version)> toRelease)
    {
        PreSetupOutput output = new()
        {
            HasRelease = toRelease.Count != 0,
            Releases = toRelease.ToDictionary(i => i.AppEntry.Id, i => new PreSetupOutputVersion()
            {
                AppId = i.AppEntry.Id,
                AppName = i.AppEntry.Name,
                Environment = i.Env,
                Version = i.Version.ToString() + "+build." + GitHubActions.Instance.RunId
            })
        };

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
        if (outputTestMatrix.Count == 0)
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
        File.WriteAllText(RootDirectory / ".nuke" / "temp" / "pre_setup_output.json", JsonSerializer.Serialize(output, _jsonSnakeCaseNamingOption));
        File.WriteAllText(RootDirectory / ".nuke" / "temp" / "pre_setup_output_test_matrix.json", JsonSerializer.Serialize(outputTestMatrix, _jsonSnakeCaseNamingOption));
        File.WriteAllText(RootDirectory / ".nuke" / "temp" / "pre_setup_output_build_matrix.json", JsonSerializer.Serialize(outputBuildMatrix, _jsonSnakeCaseNamingOption));
        File.WriteAllText(RootDirectory / ".nuke" / "temp" / "pre_setup_output_publish_matrix.json", JsonSerializer.Serialize(outputPublishMatrix, _jsonSnakeCaseNamingOption));
        Log.Information("PRE_SETUP_OUTPUT: {output}", JsonSerializer.Serialize(output, _jsonSnakeCaseNamingOptionIndented));
        Log.Information("PRE_SETUP_OUTPUT_TEST_MATRIX: {outputMatrix}", JsonSerializer.Serialize(outputTestMatrix, _jsonSnakeCaseNamingOptionIndented));
        Log.Information("PRE_SETUP_OUTPUT_BUILD_MATRIX: {outputMatrix}", JsonSerializer.Serialize(outputBuildMatrix, _jsonSnakeCaseNamingOptionIndented));
        Log.Information("PRE_SETUP_OUTPUT_PUBLISH_MATRIX: {outputMatrix}", JsonSerializer.Serialize(outputPublishMatrix, _jsonSnakeCaseNamingOptionIndented));
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

    private static Dictionary<string, object> AddGithubWorkflowJob(Dictionary<string, object> workflow, string id, string name, string runsOn, IEnumerable<string>? needs = null)
    {
        Dictionary<string, object> job = new()
        {
            ["name"] = name,
            ["runs-on"] = runsOn,
            ["steps"] = new List<object>()
        };
        if (needs != null && needs.Any())
        {
            job["needs"] = needs;
        }
        ((Dictionary<string, object>)workflow["jobs"])[id] = job;
        return job;
    }

    private static Dictionary<string, object> AddGithubWorkflowJob(Dictionary<string, object> workflow, string id, string name, RunsOnType buildsOnType, IEnumerable<string>? needs = null)
    {
        return AddGithubWorkflowJob(workflow, id, name, GetRunsOnGithub(buildsOnType), needs);
    }

    private static Dictionary<string, object> AddGithubWorkflowJobStep(Dictionary<string, object> job, string id = "", string name = "", string uses = "", string run = "", string _if = "")
    {
        Dictionary<string, object> step = new();
        ((List<object>)job["steps"]).Add(step);
        if (!string.IsNullOrEmpty(id))
        {
            step["id"] = id;
        }
        if (!string.IsNullOrEmpty(name))
        {
            step["name"] = name;
        }
        if (!string.IsNullOrEmpty(uses))
        {
            step["uses"] = uses;
        }
        if (!string.IsNullOrEmpty(run))
        {
            step["run"] = run;
        }
        if (!string.IsNullOrEmpty(_if))
        {
            step["if"] = _if;
        }
        return step;
    }

    private static void AddGithubWorkflowJobStepWith(Dictionary<string, object> step, string name, string value)
    {
        if (!step.TryGetValue("with", out object? withValue))
        {
            withValue = new Dictionary<string, object>();
            step["with"] = withValue;
        }
        ((Dictionary<string, object>)withValue).Add(name, value);
    }

    private static void AddGithubWorkflowJobMatrixInclude(Dictionary<string, object> job, string matrixInclude)
    {
        if (!job.TryGetValue("strategy", out object? value))
        {
            value = new Dictionary<string, object>();
            job["strategy"] = value;
        }
        if (!((Dictionary<string, object>)value).ContainsKey("matrix"))
        {
            ((Dictionary<string, object>)value)["matrix"] = new Dictionary<string, object>();
        }
        ((Dictionary<string, object>)((Dictionary<string, object>)value)["matrix"])["include"] = matrixInclude;
    }

    private static void AddGithubWorkflowJobOutput(Dictionary<string, object> job, string outputName, string fromStepId, string fromStepVariable)
    {
        if (!job.TryGetValue("outputs", out object? value))
        {
            value = new Dictionary<string, object>();
            job["outputs"] = value;
        }
        ((Dictionary<string, object>)value).Add(outputName, $"${{{{ steps.{fromStepId}.outputs.{fromStepVariable} }}}}");
    }

    private static void AddGithubWorkflowJobEnvVar(Dictionary<string, object> job, string envVarName, string envVarValue)
    {
        if (!job.TryGetValue("env", out object? value))
        {
            value = new Dictionary<string, object>();
            job["env"] = value;
        }
        ((Dictionary<string, object>)value).Add(envVarName, envVarValue);
    }

    private static void AddGithubWorkflowJobEnvVarFromNeeds(Dictionary<string, object> job, string envVarName, string needsId, string outputName)
    {
        AddGithubWorkflowJobEnvVar(job, envVarName, $"${{{{ needs.{needsId}.outputs.{outputName} }}}}");
    }

    public Target GithubWorkflow => _ => _
        .Description("Builds the cicd workflow for github")
        .Executes(() =>
        {
            GetOrFail(() => GetAppEntryConfigs(), out var appEntryConfigs);
            GetOrFail(() => GetAppEntries(), out var appEntries);
            GetOrFail(() => GetAppTestEntries(), out var appTestEntries);

            Dictionary<string, object> workflow = new()
            {
                ["name"] = "Nuke CICD Pipeline",
                ["on"] = new Dictionary<string, object>()
                {
                    { "push", new Dictionary<string, object>()
                        {
                            { "branches", new List<string> { "*" } },
                            { "tags", new List<string> { "**" } }
                        }
                    },
                    { "pull_request", new Dictionary<string, object>()
                        {
                            { "branches", new List<string> { "fix/**", "feat/**", "hotfix/**" } }
                        }
                    }
                },
                ["concurrency"] = new Dictionary<string, object>()
                {
                    { "group", "nuke-cicd" }
                },
                ["jobs"] = new Dictionary<string, object>()
            };

            List<string> needs = new() { "pre_setup" };

            // ██████████████████████████████████████
            // ██████████████ Pre Setup █████████████
            // ██████████████████████████████████████
            var preSetupJob = AddGithubWorkflowJob(workflow, "pre_setup", "Pre Setup", RunsOnType.Ubuntu2204);
            AddGithubWorkflowJobStep(preSetupJob, uses: "actions/checkout@v4");
            var cachePreSetupStep = AddGithubWorkflowJobStep(preSetupJob, uses: "actions/cache@v4");
            AddGithubWorkflowJobStepWith(cachePreSetupStep, "path", "~/.nuget/packages");
            AddGithubWorkflowJobStepWith(cachePreSetupStep, "key", $"{GetRunsOnGithub(RunsOnType.Ubuntu2204)}-nuget-pre_setup-${{{{ hashFiles('**/*.csproj') }}}}");
            AddGithubWorkflowJobStepWith(cachePreSetupStep, "restore-keys", $"{GetRunsOnGithub(RunsOnType.Ubuntu2204)}-nuget-pre_setup-");
            AddGithubWorkflowJobStep(preSetupJob, id: "setup", name: "Run Nuke PipelinePreSetup", run: $"{GetBuildScriptGithub(RunsOnType.Ubuntu2204)} PipelinePreSetup --args \"github\"");
            AddGithubWorkflowJobStep(preSetupJob, id: "PRE_SETUP_OUTPUT", name: "Output PRE_SETUP_OUTPUT", run: $"echo \"PRE_SETUP_OUTPUT=$(cat ./.nuke/temp/pre_setup_output.json)\" >> $GITHUB_OUTPUT");
            AddGithubWorkflowJobStep(preSetupJob, id: "PRE_SETUP_OUTPUT_TEST_MATRIX", name: "Output PRE_SETUP_OUTPUT_TEST_MATRIX", run: $"echo \"PRE_SETUP_OUTPUT_TEST_MATRIX=$(cat ./.nuke/temp/pre_setup_output_test_matrix.json)\" >> $GITHUB_OUTPUT");
            AddGithubWorkflowJobStep(preSetupJob, id: "PRE_SETUP_OUTPUT_BUILD_MATRIX", name: "Output PRE_SETUP_OUTPUT_BUILD_MATRIX", run: $"echo \"PRE_SETUP_OUTPUT_BUILD_MATRIX=$(cat ./.nuke/temp/pre_setup_output_build_matrix.json)\" >> $GITHUB_OUTPUT");
            AddGithubWorkflowJobStep(preSetupJob, id: "PRE_SETUP_OUTPUT_PUBLISH_MATRIX", name: "Output PRE_SETUP_OUTPUT_PUBLISH_MATRIX", run: $"echo \"PRE_SETUP_OUTPUT_PUBLISH_MATRIX=$(cat ./.nuke/temp/pre_setup_output_publish_matrix.json)\" >> $GITHUB_OUTPUT");
            AddGithubWorkflowJobOutput(preSetupJob, "PRE_SETUP_OUTPUT", "PRE_SETUP_OUTPUT", "PRE_SETUP_OUTPUT");
            AddGithubWorkflowJobOutput(preSetupJob, "PRE_SETUP_OUTPUT_TEST_MATRIX", "PRE_SETUP_OUTPUT_TEST_MATRIX", "PRE_SETUP_OUTPUT_TEST_MATRIX");
            AddGithubWorkflowJobOutput(preSetupJob, "PRE_SETUP_OUTPUT_BUILD_MATRIX", "PRE_SETUP_OUTPUT_BUILD_MATRIX", "PRE_SETUP_OUTPUT_BUILD_MATRIX");
            AddGithubWorkflowJobOutput(preSetupJob, "PRE_SETUP_OUTPUT_PUBLISH_MATRIX", "PRE_SETUP_OUTPUT_PUBLISH_MATRIX", "PRE_SETUP_OUTPUT_PUBLISH_MATRIX");

            // ██████████████████████████████████████
            // ████████████████ Test ████████████████
            // ██████████████████████████████████████
            if (appTestEntries.Count > 0)
            {
                var testJob = AddGithubWorkflowJob(workflow, "test", "Test - ${{ matrix.name }}", "${{ matrix.runs_on }}");
                testJob["needs"] = needs.ToArray();
                needs.Add("test");
                AddGithubWorkflowJobEnvVarFromNeeds(testJob, "PRE_SETUP_OUTPUT", "pre_setup", "PRE_SETUP_OUTPUT");
                AddGithubWorkflowJobMatrixInclude(testJob, "${{ fromJson(needs.pre_setup.outputs.PRE_SETUP_OUTPUT_TEST_MATRIX) }}");
                AddGithubWorkflowJobStep(testJob, uses: "actions/checkout@v4", _if: "${{ matrix.id != 'skip' }}");
                var cacheTestStep = AddGithubWorkflowJobStep(testJob, uses: "actions/cache@v4", _if: "${{ matrix.id != 'skip' }}");
                AddGithubWorkflowJobStepWith(cacheTestStep, "path", "~/.nuget/packages");
                AddGithubWorkflowJobStepWith(cacheTestStep, "key", "${{ matrix.runs_on }}-nuget-test-${{ hashFiles('**/*.csproj') }}");
                AddGithubWorkflowJobStepWith(cacheTestStep, "restore-keys", "${{ matrix.runs_on }}-nuget-test-");
                AddGithubWorkflowJobStep(testJob, name: "Run Nuke PipelineTest", run: "${{ matrix.build_script }} PipelineTest --args \"${{ matrix.ids_to_run }}\"", _if: "${{ matrix.id != 'skip' }}");
            }

            // ██████████████████████████████████████
            // ███████████████ Build ████████████████
            // ██████████████████████████████████████
            var buildJob = AddGithubWorkflowJob(workflow, "build", "Build - ${{ matrix.name }}", "${{ matrix.runs_on }}", needs.ToArray());
            AddGithubWorkflowJobEnvVarFromNeeds(buildJob, "PRE_SETUP_OUTPUT", "pre_setup", "PRE_SETUP_OUTPUT");
            AddGithubWorkflowJobMatrixInclude(buildJob, "${{ fromJson(needs.pre_setup.outputs.PRE_SETUP_OUTPUT_BUILD_MATRIX) }}");
            AddGithubWorkflowJobStep(buildJob, uses: "actions/checkout@v4");
            var cacheBuildStep = AddGithubWorkflowJobStep(buildJob, uses: "actions/cache@v4");
            AddGithubWorkflowJobStepWith(cacheBuildStep, "path", "~/.nuget/packages");
            AddGithubWorkflowJobStepWith(cacheBuildStep, "key", "${{ matrix.runs_on }}-nuget-build-${{ hashFiles('**/*.csproj') }}");
            AddGithubWorkflowJobStepWith(cacheBuildStep, "restore-keys", "${{ matrix.runs_on }}-nuget-build-");
            AddGithubWorkflowJobStep(buildJob, name: "Run Nuke PipelineBuild", run: "${{ matrix.build_script }} PipelineBuild --args \"${{ matrix.ids_to_run }}\"");
            var uploadBuildStep = AddGithubWorkflowJobStep(buildJob, name: "Upload artifacts", uses: "actions/upload-artifact@v4");
            AddGithubWorkflowJobStepWith(uploadBuildStep, "name", "${{ matrix.id }}");
            AddGithubWorkflowJobStepWith(uploadBuildStep, "path", "./.nuke/temp/output/*");
            AddGithubWorkflowJobStepWith(uploadBuildStep, "if-no-files-found", "error");
            AddGithubWorkflowJobStepWith(uploadBuildStep, "retention-days", "1");

            needs.Add("build");

            // ██████████████████████████████████████
            // ██████████████ Publish ███████████████
            // ██████████████████████████████████████
            var publishJob = AddGithubWorkflowJob(workflow, "publish", "Publish - ${{ matrix.name }}", "${{ matrix.runs_on }}", needs.ToArray());
            AddGithubWorkflowJobEnvVarFromNeeds(publishJob, "PRE_SETUP_OUTPUT", "pre_setup", "PRE_SETUP_OUTPUT");
            AddGithubWorkflowJobMatrixInclude(publishJob, "${{ fromJson(needs.pre_setup.outputs.PRE_SETUP_OUTPUT_PUBLISH_MATRIX) }}");
            AddGithubWorkflowJobStep(publishJob, uses: "actions/checkout@v4");
            var cachePublishStep = AddGithubWorkflowJobStep(publishJob, uses: "actions/cache@v4");
            AddGithubWorkflowJobStepWith(cachePublishStep, "path", "~/.nuget/packages");
            AddGithubWorkflowJobStepWith(cachePublishStep, "key", "${{ matrix.runs_on }}-nuget-publish-${{ hashFiles('**/*.csproj') }}");
            AddGithubWorkflowJobStepWith(cachePublishStep, "restore-keys", "${{ matrix.runs_on }}-nuget-publish-");
            var downloadBuildStep = AddGithubWorkflowJobStep(publishJob, name: "Download artifacts", uses: "actions/download-artifact@v4");
            AddGithubWorkflowJobStepWith(downloadBuildStep, "path", "./.nuke/temp/output");
            AddGithubWorkflowJobStepWith(downloadBuildStep, "pattern", "${{ matrix.id }}");
            AddGithubWorkflowJobStepWith(downloadBuildStep, "merge-multiple", "true");
            AddGithubWorkflowJobStep(publishJob, name: "Run Nuke PipelinePublish", run: "${{ matrix.build_script }} PipelinePublish --args \"${{ matrix.ids_to_run }}\"");

            // ██████████████████████████████████████
            // ██████████████ Release ███████████████
            // ██████████████████████████████████████
            var releaseJob = AddGithubWorkflowJob(workflow, "release", "Release", RunsOnType.Ubuntu2204, needs.ToArray());
            AddGithubWorkflowJobEnvVarFromNeeds(releaseJob, "PRE_SETUP_OUTPUT", "pre_setup", "PRE_SETUP_OUTPUT");
            AddGithubWorkflowJobStep(releaseJob, uses: "actions/checkout@v4");
            var cacheReleaseStep = AddGithubWorkflowJobStep(releaseJob, uses: "actions/cache@v4");
            AddGithubWorkflowJobStepWith(cacheReleaseStep, "path", "~/.nuget/packages");
            AddGithubWorkflowJobStepWith(cacheReleaseStep, "key", "${{ matrix.runs_on }}-nuget-release-${{ hashFiles('**/*.csproj') }}");
            AddGithubWorkflowJobStepWith(cacheReleaseStep, "restore-keys", "${{ matrix.runs_on }}-nuget-release-");
            var downloadReleaseStep = AddGithubWorkflowJobStep(releaseJob, name: "Download artifacts", uses: "actions/download-artifact@v4");
            AddGithubWorkflowJobStepWith(downloadReleaseStep, "path", "./.nuke/temp/output");
            AddGithubWorkflowJobStep(releaseJob, name: "Run Nuke PipelineRelease", run: $"{GetBuildScriptGithub(RunsOnType.Ubuntu2204)} PipelineRelease");

            needs.Add("publish");
            needs.Add("release");

            // ██████████████████████████████████████
            // █████████████ Post Setup █████████████
            // ██████████████████████████████████████
            var postSetupJob = AddGithubWorkflowJob(workflow, "post_setup", $"Post Setup", RunsOnType.Ubuntu2204, needs.ToArray());
            AddGithubWorkflowJobEnvVarFromNeeds(postSetupJob, "PRE_SETUP_OUTPUT", "pre_setup", "PRE_SETUP_OUTPUT");
            AddGithubWorkflowJobStep(postSetupJob, uses: "actions/checkout@v4");

            // ██████████████████████████████████████
            // ███████████████ Write ████████████████
            // ██████████████████████████████████████
            var workflowDirPath = RootDirectory / ".github" / "workflows";
            var workflowPath = workflowDirPath / "nuke-cicd.yml";

            Directory.CreateDirectory(workflowDirPath);
            File.WriteAllText(workflowPath, _yamlSerializer.Serialize(workflow));

            Log.Information("Workflow built at " + workflowPath.ToString());
        });
}
