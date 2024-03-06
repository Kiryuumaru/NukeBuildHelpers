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

internal class GithubPipeline(BaseNukeBuildHelpers nukeBuild) : IPipeline
{
    public BaseNukeBuildHelpers NukeBuild { get; set; } = nukeBuild;

    public long GetBuildId()
    {
        return GitHubActions.Instance.RunNumber;
    }

    public PipelineInfo GetPipelineInfo()
    {
        TriggerType triggerType = TriggerType.Commit;
        var branch = Environment.GetEnvironmentVariable("GITHUB_REF");
        if (string.IsNullOrEmpty(branch))
        {
            branch = NukeBuild.Repository.Branch;
        }
        else
        {
            if (branch.StartsWith("refs/pull", StringComparison.OrdinalIgnoreCase))
            {
                triggerType = TriggerType.PullRequest;
                branch = Environment.GetEnvironmentVariable("GITHUB_BASE_REF")!;
            }
            else
            {
                if (branch.StartsWith("refs/tags", StringComparison.OrdinalIgnoreCase))
                {
                    triggerType = TriggerType.Tag;
                }
                branch = NukeBuild.Git.Invoke($"branch -r --contains {branch}").FirstOrDefault().Text;
            }
            branch = branch[(branch.IndexOf('/') + 1)..];
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
        BaseNukeBuildHelpers.GetOrFail(() => BaseNukeBuildHelpers.GetAppEntryConfigs(), out var appEntryConfigs);
        BaseNukeBuildHelpers.GetOrFail(() => BaseNukeBuildHelpers.GetEntries<AppEntry>(), out var appEntries);
        BaseNukeBuildHelpers.GetOrFail(() => BaseNukeBuildHelpers.GetEntries<AppTestEntry>(), out var appTestEntries);

        var appEntrySecretMap = BaseNukeBuildHelpers.GetEntrySecretMap<AppEntry>();
        var appTestEntrySecretMap = BaseNukeBuildHelpers.GetEntrySecretMap<AppTestEntry>();

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
                            { "branches", new List<string> { "**" } }
                        }
                    }
                },
            ["concurrency"] = new Dictionary<string, object>()
                {
                    { "group", "${{ github.workflow }}-${{ github.head_ref || github.ref_name }}" },
                    { "cancel-in-progress", "true" }
                },
            ["jobs"] = new Dictionary<string, object>()
        };

        List<string> needs = ["pre_setup"];

        // ██████████████████████████████████████
        // ██████████████ Pre Setup █████████████
        // ██████████████████████████████████████
        var preSetupJob = AddJob(workflow, "pre_setup", "Pre Setup", RunsOnType.Ubuntu2204);
        AddJobStepCheckout(preSetupJob, fetchDepth: 0);
        AddJobStepNugetCache(preSetupJob, GetRunsOnGithub(RunsOnType.Ubuntu2204), "pre_setup");
        var nukePreSetupStep = AddJobStep(preSetupJob, id: "setup", name: "Run Nuke PipelinePreSetup", run: $"{GetBuildScriptGithub(RunsOnType.Ubuntu2204)} PipelinePreSetup --args \"github\"");
        AddJobOrStepEnvVar(nukePreSetupStep, "GITHUB_TOKEN", "${{ secrets.GITHUB_TOKEN }}");
        AddJobStep(preSetupJob, id: "PRE_SETUP_HAS_RELEASE", name: "Output PRE_SETUP_HAS_RELEASE", run: $"echo \"PRE_SETUP_HAS_RELEASE=$(cat ./.nuke/temp/pre_setup_has_release.txt)\" >> $GITHUB_OUTPUT");
        AddJobStep(preSetupJob, id: "PRE_SETUP_OUTPUT", name: "Output PRE_SETUP_OUTPUT", run: $"echo \"PRE_SETUP_OUTPUT=$(cat ./.nuke/temp/pre_setup_output.json)\" >> $GITHUB_OUTPUT");
        AddJobStep(preSetupJob, id: "PRE_SETUP_OUTPUT_TEST_MATRIX", name: "Output PRE_SETUP_OUTPUT_TEST_MATRIX", run: $"echo \"PRE_SETUP_OUTPUT_TEST_MATRIX=$(cat ./.nuke/temp/pre_setup_output_test_matrix.json)\" >> $GITHUB_OUTPUT");
        AddJobStep(preSetupJob, id: "PRE_SETUP_OUTPUT_BUILD_MATRIX", name: "Output PRE_SETUP_OUTPUT_BUILD_MATRIX", run: $"echo \"PRE_SETUP_OUTPUT_BUILD_MATRIX=$(cat ./.nuke/temp/pre_setup_output_build_matrix.json)\" >> $GITHUB_OUTPUT");
        AddJobStep(preSetupJob, id: "PRE_SETUP_OUTPUT_PUBLISH_MATRIX", name: "Output PRE_SETUP_OUTPUT_PUBLISH_MATRIX", run: $"echo \"PRE_SETUP_OUTPUT_PUBLISH_MATRIX=$(cat ./.nuke/temp/pre_setup_output_publish_matrix.json)\" >> $GITHUB_OUTPUT");
        AddJobOutput(preSetupJob, "PRE_SETUP_HAS_RELEASE", "PRE_SETUP_HAS_RELEASE", "PRE_SETUP_HAS_RELEASE");
        AddJobOutput(preSetupJob, "PRE_SETUP_OUTPUT", "PRE_SETUP_OUTPUT", "PRE_SETUP_OUTPUT");
        AddJobOutput(preSetupJob, "PRE_SETUP_OUTPUT_TEST_MATRIX", "PRE_SETUP_OUTPUT_TEST_MATRIX", "PRE_SETUP_OUTPUT_TEST_MATRIX");
        AddJobOutput(preSetupJob, "PRE_SETUP_OUTPUT_BUILD_MATRIX", "PRE_SETUP_OUTPUT_BUILD_MATRIX", "PRE_SETUP_OUTPUT_BUILD_MATRIX");
        AddJobOutput(preSetupJob, "PRE_SETUP_OUTPUT_PUBLISH_MATRIX", "PRE_SETUP_OUTPUT_PUBLISH_MATRIX", "PRE_SETUP_OUTPUT_PUBLISH_MATRIX");

        // ██████████████████████████████████████
        // ████████████████ Test ████████████████
        // ██████████████████████████████████████
        if (appTestEntries.Count > 0)
        {
            var testJob = AddJob(workflow, "test", "Test - ${{ matrix.name }}", "${{ matrix.runs_on }}");
            testJob["needs"] = needs.ToArray();
            needs.Add("test");
            AddJobMatrixInclude(testJob, "${{ fromJson(needs.pre_setup.outputs.PRE_SETUP_OUTPUT_TEST_MATRIX) }}");
            AddJobStepCheckout(testJob, _if: "${{ matrix.id != 'skip' }}");
            AddJobStepNugetCache(testJob, "${{ matrix.runs_on }}", "test");
            var nukeTestStep = AddJobStep(testJob, name: "Run Nuke PipelineTest", run: "${{ matrix.build_script }} PipelineTest --args \"${{ matrix.ids_to_run }}\"", _if: "${{ matrix.id != 'skip' }}");
            AddJobOrStepEnvVarFromNeeds(nukeTestStep, "PRE_SETUP_OUTPUT", "pre_setup", "PRE_SETUP_OUTPUT");
            foreach (var map in appTestEntrySecretMap)
            {
                foreach (var secrets in map.Value.SecretHelpers)
                {
                    AddJobOrStepEnvVar(nukeTestStep, secrets.SecretHelper.Name, $"${{{{ secrets.{secrets.SecretHelper.Name} }}}}");
                }
            }
        }

        // ██████████████████████████████████████
        // ███████████████ Build ████████████████
        // ██████████████████████████████████████
        var buildJob = AddJob(workflow, "build", "Build - ${{ matrix.name }}", "${{ matrix.runs_on }}", [.. needs], _if: "${{ needs.pre_setup.outputs.PRE_SETUP_HAS_RELEASE == 'true' }}");
        AddJobMatrixInclude(buildJob, "${{ fromJson(needs.pre_setup.outputs.PRE_SETUP_OUTPUT_BUILD_MATRIX) }}");
        AddJobStepCheckout(buildJob);
        AddJobStepNugetCache(buildJob, "${{ matrix.runs_on }}", "build");
        var nukeBuildStep = AddJobStep(buildJob, name: "Run Nuke PipelineBuild", run: "${{ matrix.build_script }} PipelineBuild --args \"${{ matrix.ids_to_run }}\"");
        AddJobOrStepEnvVarFromNeeds(nukeBuildStep, "PRE_SETUP_OUTPUT", "pre_setup", "PRE_SETUP_OUTPUT");
        foreach (var map in appEntrySecretMap)
        {
            foreach (var secrets in map.Value.SecretHelpers)
            {
                AddJobOrStepEnvVar(nukeBuildStep, secrets.SecretHelper.Name, $"${{{{ secrets.{secrets.SecretHelper.Name} }}}}");
            }
        }
        var uploadBuildStep = AddJobStep(buildJob, name: "Upload artifacts", uses: "actions/upload-artifact@v4");
        AddJobStepWith(uploadBuildStep, "name", "${{ matrix.id }}");
        AddJobStepWith(uploadBuildStep, "path", "./.nuke/temp/output/*");
        AddJobStepWith(uploadBuildStep, "if-no-files-found", "error");
        AddJobStepWith(uploadBuildStep, "retention-days", "1");

        needs.Add("build");

        // ██████████████████████████████████████
        // ██████████████ Publish ███████████████
        // ██████████████████████████████████████
        var publishJob = AddJob(workflow, "publish", "Publish - ${{ matrix.name }}", "${{ matrix.runs_on }}", [.. needs], _if: "${{ needs.pre_setup.outputs.PRE_SETUP_HAS_RELEASE == 'true' }}");
        AddJobMatrixInclude(publishJob, "${{ fromJson(needs.pre_setup.outputs.PRE_SETUP_OUTPUT_PUBLISH_MATRIX) }}");
        AddJobStepCheckout(publishJob);
        AddJobStepNugetCache(publishJob, "${{ matrix.runs_on }}", "publish");
        var downloadBuildStep = AddJobStep(publishJob, name: "Download artifacts", uses: "actions/download-artifact@v4");
        AddJobStepWith(downloadBuildStep, "path", "./.nuke/temp/output");
        AddJobStepWith(downloadBuildStep, "pattern", "${{ matrix.id }}");
        AddJobStepWith(downloadBuildStep, "merge-multiple", "true");
        var nukePublishStep = AddJobStep(publishJob, name: "Run Nuke PipelinePublish", run: "${{ matrix.build_script }} PipelinePublish --args \"${{ matrix.ids_to_run }}\"");
        AddJobOrStepEnvVarFromNeeds(nukePublishStep, "PRE_SETUP_OUTPUT", "pre_setup", "PRE_SETUP_OUTPUT");
        foreach (var map in appEntrySecretMap)
        {
            foreach (var secrets in map.Value.SecretHelpers)
            {
                AddJobOrStepEnvVar(nukePublishStep, secrets.SecretHelper.Name, $"${{{{ secrets.{secrets.SecretHelper.Name} }}}}");
            }
        }
        AddJobStep(publishJob, id: "PUBLISH_OUTPUT_SUCCESS", name: "Output PUBLISH_OUTPUT_SUCCESS", run: $"echo \"PUBLISH_OUTPUT_SUCCESS=$(cat ./.nuke/temp/publish_success.txt)\" >> $GITHUB_OUTPUT");
        AddJobOutput(publishJob, "PUBLISH_OUTPUT_SUCCESS", "PUBLISH_OUTPUT_SUCCESS", "PUBLISH_OUTPUT_SUCCESS");

        needs.Add("publish");

        // ██████████████████████████████████████
        // █████████████ Post Setup █████████████
        // ██████████████████████████████████████
        var postSetupJob = AddJob(workflow, "post_setup", $"Post Setup", RunsOnType.Ubuntu2204, [.. needs], _if: "success() || failure() || always()");
        AddJobStepCheckout(postSetupJob);
        AddJobStepNugetCache(postSetupJob, GetRunsOnGithub(RunsOnType.Ubuntu2204), "post_setup");
        var downloadPostSetupStep = AddJobStep(postSetupJob, name: "Download artifacts", uses: "actions/download-artifact@v4");
        AddJobStepWith(downloadPostSetupStep, "path", "./.nuke/temp/output");
        var nukePostSetupStep = AddJobStep(postSetupJob, name: "Run Nuke PipelinePostSetup", run: $"{GetBuildScriptGithub(RunsOnType.Ubuntu2204)} PipelinePostSetup");
        AddJobOrStepEnvVar(nukePostSetupStep, "GITHUB_TOKEN", "${{ secrets.GITHUB_TOKEN }}");
        AddJobOrStepEnvVarFromNeeds(nukePostSetupStep, "PRE_SETUP_OUTPUT", "pre_setup", "PRE_SETUP_OUTPUT");
        AddJobOrStepEnvVarFromNeeds(nukePostSetupStep, "PUBLISH_OUTPUT_SUCCESS", "publish", "PUBLISH_OUTPUT_SUCCESS");

        // ██████████████████████████████████████
        // ███████████████ Write ████████████████
        // ██████████████████████████████████████
        var workflowDirPath = Nuke.Common.NukeBuild.RootDirectory / ".github" / "workflows";
        var workflowPath = workflowDirPath / "nuke-cicd.yml";

        Directory.CreateDirectory(workflowDirPath);
        File.WriteAllText(workflowPath, YamlExtension.Serialize(workflow));

        Log.Information("Workflow built at " + workflowPath.ToString());
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

    private static Dictionary<string, object> AddJob(Dictionary<string, object> workflow, string id, string name, string runsOn, IEnumerable<string>? needs = null, string _if = "")
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
        if (!string.IsNullOrEmpty(_if))
        {
            job["if"] = _if;
        }
        ((Dictionary<string, object>)workflow["jobs"])[id] = job;
        return job;
    }

    private static Dictionary<string, object> AddJob(Dictionary<string, object> workflow, string id, string name, RunsOnType buildsOnType, IEnumerable<string>? needs = null, string _if = "")
    {
        return AddJob(workflow, id, name, GetRunsOnGithub(buildsOnType), needs, _if);
    }

    private static Dictionary<string, object> AddJobStep(Dictionary<string, object> job, string id = "", string name = "", string uses = "", string run = "", string _if = "")
    {
        Dictionary<string, object> step = [];
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

    private static Dictionary<string, object> AddJobStepCheckout(Dictionary<string, object> job, string _if = "", int? fetchDepth = null)
    {
        var step = AddJobStep(job, uses: "actions/checkout@v4", _if: _if);
        if (fetchDepth != null)
        {
            AddJobStepWith(step, "fetch-depth", fetchDepth.Value.ToString());
        }
        return step;
    }

    private static Dictionary<string, object> AddJobStepNugetCache(Dictionary<string, object> job, string keyRoot, string keyName, string _if = "")
    {
        var step = AddJobStep(job, uses: "actions/cache@v4", _if: _if);
        AddJobStepWith(step, "path", "~/.nuget/packages");
        AddJobStepWith(step, "key", $"{keyRoot}-nuget-{keyName}-${{{{ hashFiles('**/*.csproj') }}}}");
        AddJobStepWith(step, "restore-keys", $"{keyRoot}-nuget-{keyName}-");
        return step;
    }

    private static void AddJobStepWith(Dictionary<string, object> step, string name, string value)
    {
        if (!step.TryGetValue("with", out object? withValue))
        {
            withValue = new Dictionary<string, object>();
            step["with"] = withValue;
        }
        ((Dictionary<string, object>)withValue)[name] = value;
    }

    private static void AddJobMatrixInclude(Dictionary<string, object> job, string matrixInclude)
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

    private static void AddJobOutput(Dictionary<string, object> job, string outputName, string fromStepId, string fromStepVariable)
    {
        if (!job.TryGetValue("outputs", out object? value))
        {
            value = new Dictionary<string, object>();
            job["outputs"] = value;
        }
        ((Dictionary<string, object>)value)[outputName] = $"${{{{ steps.{fromStepId}.outputs.{fromStepVariable} }}}}";
    }

    private static void AddJobOrStepEnvVar(Dictionary<string, object> jobOrStep, string envVarName, string envVarValue)
    {
        if (!jobOrStep.TryGetValue("env", out object? value))
        {
            value = new Dictionary<string, object>();
            jobOrStep["env"] = value;
        }
        ((Dictionary<string, object>)value)[envVarName] = envVarValue;
    }

    private static void AddJobOrStepEnvVarFromNeeds(Dictionary<string, object> jobOrStep, string envVarName, string needsId, string outputName)
    {
        AddJobOrStepEnvVar(jobOrStep, envVarName, $"${{{{ needs.{needsId}.outputs.{outputName} }}}}");
    }
}
