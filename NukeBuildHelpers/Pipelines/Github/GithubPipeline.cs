using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Identity.Client;
using Nuke.Common;
using Nuke.Common.IO;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Common.Attributes;
using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Common.Models;
using NukeBuildHelpers.Entry;
using NukeBuildHelpers.Entry.Helpers;
using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.Pipelines.Common.Enums;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.Pipelines.Common.Models;
using NukeBuildHelpers.Pipelines.Github.Models;
using NukeBuildHelpers.Runner.Abstraction;
using NukeBuildHelpers.Runner.Models;
using Octokit;
using Serilog;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;

namespace NukeBuildHelpers.Pipelines.Github;

internal class GithubPipeline(BaseNukeBuildHelpers nukeBuild) : IPipeline
{
    public const string Id = "github";

    public BaseNukeBuildHelpers NukeBuild { get; set; } = nukeBuild;

    public PipelineInfo GetPipelineInfo()
    {
        TriggerType triggerType = TriggerType.Commit;
        var branch = Environment.GetEnvironmentVariable("GITHUB_REF");
        long prNumber = 0;
        if (string.IsNullOrEmpty(branch))
        {
            branch = NukeBuild.Repository.Branch;
        }
        else
        {
            if (branch.StartsWith("refs/pull", StringComparison.InvariantCultureIgnoreCase))
            {
                prNumber = long.Parse(branch.Split('/')[2]);
                triggerType = TriggerType.PullRequest;
                branch = Environment.GetEnvironmentVariable("GITHUB_BASE_REF")!;
            }
            else if (branch.StartsWith("refs/tags", StringComparison.InvariantCultureIgnoreCase))
            {
                triggerType = TriggerType.Tag;
                if (branch.StartsWith("refs/tags/bump-"))
                {
                    branch = branch[15..];
                }
                else
                {
                    branch = NukeBuild.Git.Invoke($"branch -r --contains {branch}").FirstOrDefault().Text;
                    branch = branch[(branch.IndexOf('/') + 1)..];
                }
            }
            else if (branch.StartsWith("refs/heads", StringComparison.InvariantCultureIgnoreCase))
            {
                triggerType = TriggerType.Commit;
                branch = branch[11..];
            }
        }
        return new()
        {
            Branch = branch,
            TriggerType = triggerType,
            PullRequestNumber = prNumber,
        };
    }

    public Task PreSetup(AllEntry allEntry, PipelinePreSetup pipelinePreSetup)
    {
        var outputTestMatrix = new List<GithubPreSetupOutputAppTestEntryMatrix>();
        var outputBuildMatrix = new List<GithubPreSetupOutputAppEntryMatrix>();
        var outputPublishMatrix = new List<GithubPreSetupOutputAppEntryMatrix>();

        var runClassification = pipelinePreSetup.TriggerType == TriggerType.PullRequest ? "pr." + pipelinePreSetup.PullRequestNumber : "main";
        var runIdentifier = Guid.NewGuid().Encode();

        foreach (var entryId in pipelinePreSetup.TestEntries)
        {
            if (!allEntry.TestEntryDefinitionMap.TryGetValue(entryId, out var appEntryDefinition))
            {
                continue;
            }
            if (!pipelinePreSetup.EntrySetupMap.TryGetValue(entryId, out var entrySetup))
            {
                continue;
            }
            RunnerGithubPipelineOS runnerPipelineOS = JsonSerializer.Deserialize<RunnerGithubPipelineOS>(entrySetup.RunnerOSSetup.RunnerPipelineOS, JsonExtension.SnakeCaseNamingOptionIndented)!;
            string? runsOn = string.IsNullOrEmpty(runnerPipelineOS.RunsOn) ? "[ " + string.Join(", ", runnerPipelineOS.RunsOnLabels!) + " ]" : runnerPipelineOS.RunsOn;
            outputTestMatrix.Add(new()
            {
                NukeEntryId = appEntryDefinition.Id,
                NukeEntryName = entrySetup.Name,
                NukeEnvironment = pipelinePreSetup.Environment,
                NukeRunsOn = runsOn,
                NukeRunScript = entrySetup.RunnerOSSetup.RunScript,
                NukeEntryIdsToRun = entryId,
                NukeCacheInvalidator = entrySetup.CacheInvalidator,
                NukeRunClassification = runClassification,
                NukeRunIdentifier = runIdentifier
            });
        }

        foreach (var entryId in pipelinePreSetup.BuildEntries)
        {
            if (!allEntry.BuildEntryDefinitionMap.TryGetValue(entryId, out var appEntryDefinition))
            {
                continue;
            }
            if (!pipelinePreSetup.EntrySetupMap.TryGetValue(entryId, out var entrySetup))
            {
                continue;
            }
            if (!pipelinePreSetup.AppRunEntryMap.TryGetValue(appEntryDefinition.AppId.NotNullOrEmpty().ToLowerInvariant(), out var appRunEntry))
            {
                continue;
            }

            RunnerGithubPipelineOS runnerPipelineOS = JsonSerializer.Deserialize<RunnerGithubPipelineOS>(entrySetup.RunnerOSSetup.RunnerPipelineOS, JsonExtension.SnakeCaseNamingOptionIndented)!;
            string? runsOn = string.IsNullOrEmpty(runnerPipelineOS.RunsOn) ? "[ " + string.Join(", ", runnerPipelineOS.RunsOnLabels!) + " ]" : runnerPipelineOS.RunsOn;
            outputBuildMatrix.Add(new()
            {
                NukeEntryId = appEntryDefinition.Id,
                NukeEntryName = entrySetup.Name,
                NukeEnvironment = pipelinePreSetup.Environment,
                NukeRunsOn = runsOn,
                NukeRunScript = entrySetup.RunnerOSSetup.RunScript,
                NukeEntryIdsToRun = entryId,
                NukeCacheInvalidator = entrySetup.CacheInvalidator,
                NukeRunClassification = runClassification,
                NukeRunIdentifier = runIdentifier,
                NukeVersion = appRunEntry.Version,
            });
        }

        foreach (var entryId in pipelinePreSetup.PublishEntries)
        {
            if (!allEntry.PublishEntryDefinitionMap.TryGetValue(entryId, out var appEntryDefinition))
            {
                continue;
            }
            if (!pipelinePreSetup.EntrySetupMap.TryGetValue(entryId, out var entrySetup))
            {
                continue;
            }
            if (!pipelinePreSetup.AppRunEntryMap.TryGetValue(appEntryDefinition.AppId.NotNullOrEmpty().ToLowerInvariant(), out var appRunEntry))
            {
                continue;
            }

            RunnerGithubPipelineOS runnerPipelineOS = JsonSerializer.Deserialize<RunnerGithubPipelineOS>(entrySetup.RunnerOSSetup.RunnerPipelineOS, JsonExtension.SnakeCaseNamingOptionIndented)!;
            string? runsOn = string.IsNullOrEmpty(runnerPipelineOS.RunsOn) ? "[ " + string.Join(", ", runnerPipelineOS.RunsOnLabels!) + " ]" : runnerPipelineOS.RunsOn;
            outputPublishMatrix.Add(new()
            {
                NukeEntryId = appEntryDefinition.Id,
                NukeEntryName = entrySetup.Name,
                NukeEnvironment = pipelinePreSetup.Environment,
                NukeRunsOn = runsOn,
                NukeRunScript = entrySetup.RunnerOSSetup.RunScript,
                NukeEntryIdsToRun = entryId,
                NukeCacheInvalidator = entrySetup.CacheInvalidator,
                NukeRunClassification = runClassification,
                NukeRunIdentifier = runIdentifier,
                NukeVersion = appRunEntry.Version,
            });
        }

        return Task.Run(() =>
        {
            File.WriteAllText(Nuke.Common.NukeBuild.TemporaryDirectory / "pre_setup_output_test_matrix.json", JsonSerializer.Serialize(outputTestMatrix, JsonExtension.SnakeCaseNamingOption));
            File.WriteAllText(Nuke.Common.NukeBuild.TemporaryDirectory / "pre_setup_output_build_matrix.json", JsonSerializer.Serialize(outputBuildMatrix, JsonExtension.SnakeCaseNamingOption));
            File.WriteAllText(Nuke.Common.NukeBuild.TemporaryDirectory / "pre_setup_output_publish_matrix.json", JsonSerializer.Serialize(outputPublishMatrix, JsonExtension.SnakeCaseNamingOption));
            Log.Information("NUKE_PRE_SETUP_OUTPUT_TEST_MATRIX: {outputMatrix}", JsonSerializer.Serialize(outputTestMatrix, JsonExtension.SnakeCaseNamingOptionIndented));
            Log.Information("NUKE_PRE_SETUP_OUTPUT_BUILD_MATRIX: {outputMatrix}", JsonSerializer.Serialize(outputBuildMatrix, JsonExtension.SnakeCaseNamingOptionIndented));
            Log.Information("NUKE_PRE_SETUP_OUTPUT_PUBLISH_MATRIX: {outputMatrix}", JsonSerializer.Serialize(outputPublishMatrix, JsonExtension.SnakeCaseNamingOptionIndented));
        });
    }

    public void EntrySetup(AllEntry allEntry, PipelinePreSetup pipelinePreSetup)
    {
        throw new NotImplementedException();
    }

    public void BuildWorkflow(BaseNukeBuildHelpers baseNukeBuildHelpers, AllEntry allEntry)
    {
        Dictionary<string, object> workflow = new()
        {
            ["name"] = "Nuke CICD Pipeline",
            ["on"] = new Dictionary<string, object>()
                {
                    { "push", new Dictionary<string, object>()
                        {
                            { "branches", NukeBuild.EnvironmentBranches.ToArray() },
                            { "tags", new List<string> { "bump-*" } }
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
                    { "group", "${{ github.workflow }}-${{ github.ref }}" },
                    { "cancel-in-progress", true }
                },
            ["jobs"] = new Dictionary<string, object>(),
            ["env"] = EntryHelpers.GetSecretVariables(baseNukeBuildHelpers)
                .ToDictionary(
                    i => string.IsNullOrEmpty(i.Secret.EnvironmentVariableName) ? $"NUKE_{i.Secret.SecretVariableName}" : i.Secret.EnvironmentVariableName,
                    i => (object)$"${{{{ secrets.{i.Secret.SecretVariableName} }}}}")
        };

        List<string> needs = [];

        // ██████████████████████████████████████
        // ██████████████ Pre Setup █████████████
        // ██████████████████████████████████████
        var preSetupJob = AddJob(workflow, "pre_setup", "Pre Setup", RunnerOS.Ubuntu2204);
        AddJobOrStepEnvVar(preSetupJob, "GITHUB_TOKEN", "${{ secrets.GITHUB_TOKEN }}");
        AddJobStepCheckout(preSetupJob, fetchDepth: 0);
        AddJobStepNukeRun(preSetupJob, RunnerOS.Ubuntu2204, "PipelinePreSetup", Id);
        AddJobOutputFromFile(preSetupJob, "NUKE_PRE_SETUP_HAS_RELEASE", "./.nuke/temp/pre_setup_has_release.txt");
        AddJobOutputFromFile(preSetupJob, "NUKE_PRE_SETUP_HAS_ENTRIES", "./.nuke/temp/pre_setup_has_entries.txt");
        AddJobOutputFromFile(preSetupJob, "NUKE_PRE_SETUP_HAS_TEST", "./.nuke/temp/pre_setup_has_test.txt");
        AddJobOutputFromFile(preSetupJob, "NUKE_PRE_SETUP_HAS_BUILD", "./.nuke/temp/pre_setup_has_build.txt");
        AddJobOutputFromFile(preSetupJob, "NUKE_PRE_SETUP_HAS_PUBLISH", "./.nuke/temp/pre_setup_has_publish.txt");
        AddJobOutputFromFile(preSetupJob, "NUKE_PRE_SETUP_OUTPUT", "./.nuke/temp/pre_setup_output.json");
        AddJobOutputFromFile(preSetupJob, "NUKE_PRE_SETUP_OUTPUT_TEST_MATRIX", "./.nuke/temp/pre_setup_output_test_matrix.json");
        AddJobOutputFromFile(preSetupJob, "NUKE_PRE_SETUP_OUTPUT_BUILD_MATRIX", "./.nuke/temp/pre_setup_output_build_matrix.json");
        AddJobOutputFromFile(preSetupJob, "NUKE_PRE_SETUP_OUTPUT_PUBLISH_MATRIX", "./.nuke/temp/pre_setup_output_publish_matrix.json");
        needs.Add("pre_setup");

        // ██████████████████████████████████████
        // ████████████████ Test ████████████████
        // ██████████████████████████████████████
        List<string> testNeeds = [.. needs];
        foreach (var entryDefinition in allEntry.TestEntryDefinitionMap.Values)
        {
            var testJob = AddJob(workflow, entryDefinition.Id, "Test - ${{ matrix.nuke_entry_name }}", "${{ matrix.nuke_runs_on }}", needs: [.. testNeeds], _if: "success()");
            AddJobOrStepEnvVarFromNeeds(testJob, "NUKE_PRE_SETUP_OUTPUT", "pre_setup");
            AddJobMatrixIncludeFromPreSetup(testJob, "NUKE_PRE_SETUP_OUTPUT_TEST_MATRIX");
            AddJobStepCheckout(testJob, _if: "${{ matrix.nuke_entry_id != 'skip' }}");
            //AddJobStepsFromBuilder(testJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPreTestRun(step));
            var cacheTestStep = AddJobStep(testJob, name: "Cache Test", uses: "actions/cache@v4", _if: "${{ matrix.nuke_entry_id != 'skip' }}");
            AddJobStepWith(cacheTestStep, "path", "./.nuke/cache");
            AddJobStepWith(cacheTestStep, "key", $$$"""
                test-${{ matrix.nuke_runner_name }}-${{ matrix.nuke_entry_id }}-${{ matrix.nuke_cache_invalidator }}-${{ matrix.nuke_environment }}-${{ matrix.nuke_run_classification }}-${{ matrix.nuke_run_identifier }}"
                """);
            AddJobStepWith(cacheTestStep, "restore-keys", $$$"""
                test-${{ matrix.nuke_runner_name }}-${{ matrix.nuke_entry_id }}-${{ matrix.nuke_cache_invalidator }}-${{ matrix.nuke_environment }}-${{ matrix.nuke_run_classification }}-
                test-${{ matrix.nuke_runner_name }}-${{ matrix.nuke_entry_id }}-${{ matrix.nuke_cache_invalidator }}-${{ matrix.nuke_environment }}-main-
                """);
            var nukeTestStep = AddJobStepNukeRun(testJob, "${{ matrix.nuke_run_script }}", "PipelineTest", "${{ matrix.nuke_entry_ids_to_run }}", _if: "${{ matrix.nuke_entry_id != 'skip' }}");
            //AddJobStepsFromBuilder(testJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPostTestRun(step));
            needs.Add(entryDefinition.Id);
        }

        // ██████████████████████████████████████
        // ███████████████ Build ████████████████
        // ██████████████████████████████████████
        List<string> buildNeeds = [.. needs];
        foreach (var entryDefinition in allEntry.TestEntryDefinitionMap.Values)
        {
            var buildJob = AddJob(workflow, entryDefinition.Id, "Build - ${{ matrix.nuke_entry_name }}", "${{ matrix.nuke_runs_on }}", needs: [.. buildNeeds], _if: "success()");
            AddJobOrStepEnvVarFromNeeds(buildJob, "NUKE_PRE_SETUP_OUTPUT", "pre_setup");
            AddJobMatrixIncludeFromPreSetup(buildJob, "NUKE_PRE_SETUP_OUTPUT_BUILD_MATRIX");
            AddJobStepCheckout(buildJob, _if: "${{ matrix.nuke_entry_id != 'skip' }}");
            //AddJobStepsFromBuilder(buildJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPreBuildRun(step));
            var cacheBuildStep = AddJobStep(buildJob, name: "Cache Build", uses: "actions/cache@v4", _if: "${{ matrix.nuke_entry_id != 'skip' }}");
            AddJobStepWith(cacheBuildStep, "path", "./.nuke/cache");
            AddJobStepWith(cacheBuildStep, "key", $$$"""
                build-${{ matrix.nuke_runner_name }}-${{ matrix.nuke_entry_id }}-${{ matrix.nuke_cache_invalidator }}-${{ matrix.nuke_environment }}-${{ matrix.nuke_run_classification }}-${{ matrix.nuke_run_identifier }}"
                """);
            AddJobStepWith(cacheBuildStep, "restore-keys", $$$"""
                build-${{ matrix.nuke_runner_name }}-${{ matrix.nuke_entry_id }}-${{ matrix.nuke_cache_invalidator }}-${{ matrix.nuke_environment }}-${{ matrix.nuke_run_classification }}-
                build-${{ matrix.nuke_runner_name }}-${{ matrix.nuke_entry_id }}-${{ matrix.nuke_cache_invalidator }}-${{ matrix.nuke_environment }}-main-
                """);
            var nukeBuild = AddJobStepNukeRun(buildJob, "${{ matrix.nuke_run_script }}", "PipelineBuild", "${{ matrix.nuke_entry_ids_to_run }}", _if: "${{ matrix.nuke_entry_id != 'skip' }}");
            //AddJobStepsFromBuilder(buildJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPostBuildRun(step));
            var uploadBuildStep = AddJobStep(buildJob, name: "Upload Artifacts", uses: "actions/upload-artifact@v4", _if: "${{ matrix.nuke_entry_id != 'skip' }}");
            AddJobStepWith(uploadBuildStep, "name", "${{ matrix.nuke_entry_id }}");
            AddJobStepWith(uploadBuildStep, "path", "./.nuke/output/*");
            AddJobStepWith(uploadBuildStep, "if-no-files-found", "error");
            AddJobStepWith(uploadBuildStep, "retention-days", "1");
            needs.Add(entryDefinition.Id);
        }

        // ██████████████████████████████████████
        // ██████████████ Publish ███████████████
        // ██████████████████████████████████████
        List<string> publishNeeds = [.. needs];
        foreach (var entryDefinition in allEntry.TestEntryDefinitionMap.Values)
        {
            var publishJob = AddJob(workflow, entryDefinition.Id, "Publish - ${{ matrix.nuke_entry_name }}", "${{ matrix.nuke_runs_on }}", needs: [.. publishNeeds], _if: "success()");
            AddJobOrStepEnvVarFromNeeds(publishJob, "NUKE_PRE_SETUP_OUTPUT", "pre_setup");
            AddJobMatrixIncludeFromPreSetup(publishJob, "NUKE_PRE_SETUP_OUTPUT_PUBLISH_MATRIX");
            AddJobStepCheckout(publishJob, _if: "${{ matrix.nuke_entry_id != 'skip' }}");
            var downloadBuildStep = AddJobStep(publishJob, name: "Download artifacts", uses: "actions/download-artifact@v4", _if: "${{ matrix.nuke_entry_id != 'skip' }}");
            AddJobStepWith(downloadBuildStep, "path", "./.nuke/output");
            AddJobStepWith(downloadBuildStep, "pattern", "${{ matrix.nuke_entry_id }}");
            AddJobStepWith(downloadBuildStep, "merge-multiple", "true");
            //AddJobStepsFromBuilder(publishJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPrePublishRun(step));
            var cachePublishStep = AddJobStep(publishJob, name: "Cache Publish", uses: "actions/cache@v4", _if: "${{ matrix.nuke_entry_id != 'skip' }}");
            AddJobStepWith(cachePublishStep, "path", "./.nuke/cache");
            AddJobStepWith(cachePublishStep, "key", $$$"""
                publish-${{ matrix.nuke_runner_name }}-${{ matrix.nuke_entry_id }}-${{ matrix.nuke_cache_invalidator }}-${{ matrix.nuke_environment }}-${{ matrix.nuke_run_classification }}-${{ matrix.nuke_run_identifier }}"
                """);
            AddJobStepWith(cachePublishStep, "restore-keys", $$$"""
                publish-${{ matrix.nuke_runner_name }}-${{ matrix.nuke_entry_id }}-${{ matrix.nuke_cache_invalidator }}-${{ matrix.nuke_environment }}-${{ matrix.nuke_run_classification }}-
                publish-${{ matrix.nuke_runner_name }}-${{ matrix.nuke_entry_id }}-${{ matrix.nuke_cache_invalidator }}-${{ matrix.nuke_environment }}-main-
                """);
            var nukePublishTask = AddJobStepNukeRun(publishJob, "${{ matrix.nuke_run_script }}", "PipelinePublish", "${{ matrix.nuke_entry_ids_to_run }}", _if: "${{ matrix.nuke_entry_id != 'skip' }}");
            //AddJobStepsFromBuilder(publishJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPostPublishRun(step));
            needs.Add(entryDefinition.Id);
        }

        // ██████████████████████████████████████
        // █████████████ Post Setup █████████████
        // ██████████████████████████████████████
        var postSetupJob = AddJob(workflow, "post_setup", $"Post Setup", RunnerOS.Ubuntu2204, needs: [.. needs], _if: "success() || failure() || always()");
        AddJobOrStepEnvVarFromNeeds(postSetupJob, "NUKE_PRE_SETUP_OUTPUT", "pre_setup");
        AddJobOrStepEnvVar(postSetupJob, "NUKE_PUBLISH_SUCCESS_GITHUB", "${{ needs.publish.result }}");
        AddJobStep(postSetupJob, id: "NUKE_PUBLISH_SUCCESS", name: $"Resolve NUKE_PUBLISH_SUCCESS",
            run: $"echo \"NUKE_PUBLISH_SUCCESS=${{NUKE_PUBLISH_SUCCESS_GITHUB/success/ok}}\" >> $GITHUB_OUTPUT");
        AddJobStepCheckout(postSetupJob);
        var downloadPostSetupStep = AddJobStep(postSetupJob, name: "Download Artifacts", uses: "actions/download-artifact@v4");
        AddJobStepWith(downloadPostSetupStep, "path", "./.nuke/output");
        var nukePostSetup = AddJobStepNukeRun(postSetupJob, RunnerOS.Ubuntu2204, "PipelinePostSetup");
        AddJobOrStepEnvVar(nukePostSetup, "NUKE_PUBLISH_SUCCESS", "${{ steps.NUKE_PUBLISH_SUCCESS.outputs.NUKE_PUBLISH_SUCCESS }}");
        AddJobOrStepEnvVar(nukePostSetup, "GITHUB_TOKEN", "${{ secrets.GITHUB_TOKEN }}");

        // ██████████████████████████████████████
        // ███████████████ Write ████████████████
        // ██████████████████████████████████████
        var workflowDirPath = Nuke.Common.NukeBuild.RootDirectory / ".github" / "workflows";
        var workflowPath = workflowDirPath / "nuke-cicd.yml";

        Directory.CreateDirectory(workflowDirPath);
        File.WriteAllText(workflowPath, YamlExtension.Serialize(workflow));

        Log.Information("Workflow built at " + workflowPath.ToString());
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

    private static Dictionary<string, object> AddJob(Dictionary<string, object> workflow, string id, string name, RunnerOS runnerOS, IEnumerable<string>? needs = null, string _if = "")
    {
        RunnerGithubPipelineOS skipRunnerPipelineOS = (runnerOS.GetPipelineOS(PipelineType.Github) as RunnerGithubPipelineOS)!;
        string? runsOn = string.IsNullOrEmpty(skipRunnerPipelineOS.RunsOn) ? "[ " + string.Join(", ", skipRunnerPipelineOS.RunsOnLabels!) + " ]" : skipRunnerPipelineOS.RunsOn;
        return AddJob(workflow, id, name, runsOn, needs, _if);
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

    //private static void AddJobStepsFromBuilder(Dictionary<string, object> job, List<WorkflowEntry> workflowBuilders, Action<WorkflowEntry, Dictionary<string, object>> toBuild)
    //{
    //    foreach (var workflowBuilder in workflowBuilders)
    //    {
    //        Dictionary<string, object> step = [];
    //        toBuild.Invoke(workflowBuilder, step);
    //        if (step.Count > 0)
    //        {
    //            ((List<object>)job["steps"]).Add(step);
    //        }
    //    }
    //}

    private static Dictionary<string, object> AddJobStepCheckout(Dictionary<string, object> job, string _if = "", int? fetchDepth = null)
    {
        var step = AddJobStep(job, uses: "actions/checkout@v4", _if: _if);
        if (fetchDepth != null)
        {
            AddJobStepWith(step, "fetch-depth", fetchDepth.Value.ToString());
        }
        return step;
    }

    private static Dictionary<string, object> AddJobStepNukeRun(Dictionary<string, object> job, string buildScript, string targetName, string args = "", string _if = "")
    {
        var script = $"{buildScript} {targetName}";
        if (!string.IsNullOrEmpty(args))
        {
            script += $" --args \"{args}\"";
        }
        return AddJobStep(job, name: $"Run Nuke {targetName}", run: script, _if: _if);
    }

    private static Dictionary<string, object> AddJobStepNukeRun(Dictionary<string, object> job, RunnerOS runnerOS, string targetName, string args = "", string _if = "")
    {
        return AddJobStepNukeRun(job, runnerOS.GetRunScript(PipelineType.Github), targetName, args, _if);
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

    private static void AddJobMatrixIncludeFromPreSetup(Dictionary<string, object> job, string outputName)
    {
        AddJobMatrixInclude(job, $"${{{{ fromJson(needs.pre_setup.outputs.{outputName}) }}}}");
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

    private static void AddJobOrStepEnvVarFromNeeds(Dictionary<string, object> jobOrStep, string envOutName, string needsId)
    {
        AddJobOrStepEnvVar(jobOrStep, envOutName, $"${{{{ needs.{needsId}.outputs.{envOutName} }}}}");
    }

    private static void AddJobOutputFromFile(Dictionary<string, object> job, string envVarName, string filename)
    {
        AddJobStep(job, id: envVarName, name: $"Output {envVarName}", run: $"echo \"{envVarName}=$(cat {filename})\" >> $GITHUB_OUTPUT");
        AddJobOutput(job, envVarName, envVarName, envVarName);
    }
}
