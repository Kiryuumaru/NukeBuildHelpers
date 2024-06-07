using ICSharpCode.SharpZipLib.Zip;
using Nuke.Common;
using Nuke.Common.IO;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Models;
using NukeBuildHelpers.Pipelines.Enums;
using NukeBuildHelpers.Pipelines.Interfaces;
using NukeBuildHelpers.Pipelines.Models;
using Serilog;
using System.Reflection;
using System.Text.Json;

namespace NukeBuildHelpers;

internal class AzurePipeline(BaseNukeBuildHelpers nukeBuild) : IPipeline
{
    public BaseNukeBuildHelpers NukeBuild { get; set; } = nukeBuild;

    public PipelineInfo GetPipelineInfo()
    {
        TriggerType triggerType = TriggerType.Commit;
        var branch = Environment.GetEnvironmentVariable("BUILD_SOURCEBRANCH");
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
                branch = Environment.GetEnvironmentVariable("SYSTEM_PULLREQUEST_TARGETBRANCH")!;
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
            PullRequestNumber = prNumber
        };
    }

    public void Prepare(PreSetupOutput preSetupOutput, AppConfig appConfig, Dictionary<string, AppRunEntry> toEntry)
    {
        var outputTestMatrix = new Dictionary<string, AzurePreSetupOutputAppTestEntryMatrix>();
        var outputBuildMatrix = new Dictionary<string, AzurePreSetupOutputAppEntryMatrix>();
        var outputPublishMatrix = new Dictionary<string, AzurePreSetupOutputAppEntryMatrix>();

        var runClassification = preSetupOutput.TriggerType == TriggerType.PullRequest ? "pr." + preSetupOutput.PullRequestNumber : "main";
        var runIdentifier = Guid.NewGuid().Encode();

        foreach (var toTest in preSetupOutput.ToTest)
        {
            if (!appConfig.AppTestEntries.TryGetValue(toTest, out var appTestEntry))
            {
                continue;
            }
            var appEntry = appConfig.AppEntries.Values.FirstOrDefault(i => appTestEntry.AppEntryTargets.Contains(i.GetType()));
            if (appEntry == null)
            {
                continue;
            }
            RunnerAzurePipelineOS runnerPipelineOS = (appTestEntry.RunnerOS.GetPipelineOS(PipelineType.Azure) as RunnerAzurePipelineOS)!;
            string? poolName = runnerPipelineOS.PoolName;
            string? poolVMImage = runnerPipelineOS.PoolVMImage;
            string? runScript = appTestEntry.RunnerOS.GetRunScript(PipelineType.Azure);
            outputTestMatrix.Add(appTestEntry.Id, new()
            {
                NukeEntryId = appTestEntry.Id,
                NukeEntryName = appTestEntry.Name,
                NukeEnvironment = preSetupOutput.Environment,
                NukePoolName = poolName,
                NukePoolVMImage = poolVMImage,
                NukeRunScript = runScript,
                NukeEntryIdsToRun = $"{appEntry.Id};{appTestEntry.Id}",
                NukeCacheInvalidator = appEntry.CacheInvalidator,
                NukeRunClassification = runClassification,
                NukeRunIdentifier = runIdentifier
            });
        }

        foreach (var toBuild in preSetupOutput.ToBuild)
        {
            if (!appConfig.AppEntries.TryGetValue(toBuild, out var appEntry))
            {
                continue;
            }
            if (!toEntry.TryGetValue(appEntry.Id, out var entry))
            {
                continue;
            }
            RunnerAzurePipelineOS runnerPipelineOS = (appEntry.BuildRunnerOS.GetPipelineOS(PipelineType.Azure) as RunnerAzurePipelineOS)!;
            string? poolName = runnerPipelineOS.PoolName;
            string? poolVMImage = runnerPipelineOS.PoolVMImage;
            string? runScript = appEntry.BuildRunnerOS.GetRunScript(PipelineType.Azure);
            outputBuildMatrix.Add(appEntry.Id, new()
            {
                NukeEntryId = appEntry.Id,
                NukeEntryName = appEntry.Name,
                NukeEnvironment = preSetupOutput.Environment,
                NukePoolName = poolName,
                NukePoolVMImage = poolVMImage,
                NukeRunScript = runScript,
                NukeEntryIdsToRun = appEntry.Id,
                NukeVersion = entry.Version.ToString(),
                NukeCacheInvalidator = appEntry.CacheInvalidator,
                NukeRunClassification = runClassification,
                NukeRunIdentifier = runIdentifier
            });
        }

        foreach (var toPublish in preSetupOutput.ToPublish)
        {
            if (!appConfig.AppEntries.TryGetValue(toPublish, out var appEntry))
            {
                continue;
            }
            if (!toEntry.TryGetValue(appEntry.Id, out var entry))
            {
                continue;
            }
            RunnerAzurePipelineOS runnerPipelineOS = (appEntry.PublishRunnerOS.GetPipelineOS(PipelineType.Azure) as RunnerAzurePipelineOS)!;
            string? poolName = runnerPipelineOS.PoolName;
            string? poolVMImage = runnerPipelineOS.PoolVMImage;
            string? runScript = appEntry.PublishRunnerOS.GetRunScript(PipelineType.Azure);
            outputPublishMatrix.Add(appEntry.Id, new()
            {
                NukeEntryId = appEntry.Id,
                NukeEntryName = appEntry.Name,
                NukeEnvironment = preSetupOutput.Environment,
                NukePoolName = poolName,
                NukePoolVMImage = poolVMImage,
                NukeRunScript = runScript,
                NukeEntryIdsToRun = appEntry.Id,
                NukeVersion = entry.Version.ToString(),
                NukeCacheInvalidator = appEntry.CacheInvalidator,
                NukeRunClassification = runClassification,
                NukeRunIdentifier = runIdentifier
            });
        }

        RunnerAzurePipelineOS skipRunnerPipelineOS = (RunnerOS.Ubuntu2204.GetPipelineOS(PipelineType.Azure) as RunnerAzurePipelineOS)!;
        string? skipPoolName = skipRunnerPipelineOS.PoolName;
        string? skipPoolVMImage = skipRunnerPipelineOS.PoolVMImage;
        string? skipRunScript = RunnerOS.Ubuntu2204.GetRunScript(PipelineType.Azure);
        if (outputTestMatrix.Count == 0)
        {
            outputTestMatrix.Add("skip", new()
            {
                NukeEntryId = "skip",
                NukeEntryName = "Skip",
                NukeEnvironment = preSetupOutput.Environment,
                NukePoolName = skipPoolName,
                NukePoolVMImage = skipPoolVMImage,
                NukeRunScript = skipRunScript,
                NukeEntryIdsToRun = "",
                NukeCacheInvalidator = "",
                NukeRunClassification = "",
                NukeRunIdentifier = ""
            });
        }
        if (outputBuildMatrix.Count == 0)
        {
            outputBuildMatrix.Add("skip", new()
            {
                NukeEntryId = "skip",
                NukeEntryName = "Skip",
                NukeEnvironment = preSetupOutput.Environment,
                NukePoolName = skipPoolName,
                NukePoolVMImage = skipPoolVMImage,
                NukeRunScript = skipRunScript,
                NukeEntryIdsToRun = "",
                NukeVersion = "",
                NukeCacheInvalidator = "",
                NukeRunClassification = "",
                NukeRunIdentifier = ""
            });
        }
        if (outputPublishMatrix.Count == 0)
        {
            outputPublishMatrix.Add("skip", new()
            {
                NukeEntryId = "skip",
                NukeEntryName = "Skip",
                NukeEnvironment = preSetupOutput.Environment,
                NukePoolName = skipPoolName,
                NukePoolVMImage = skipPoolVMImage,
                NukeRunScript = skipRunScript,
                NukeEntryIdsToRun = "",
                NukeVersion = "",
                NukeCacheInvalidator = "",
                NukeRunClassification = "",
                NukeRunIdentifier = ""
            });
        }

        File.WriteAllText(Nuke.Common.NukeBuild.TemporaryDirectory / "pre_setup_output_test_matrix.json", JsonSerializer.Serialize(outputTestMatrix, JsonExtension.SnakeCaseNamingOption));
        File.WriteAllText(Nuke.Common.NukeBuild.TemporaryDirectory / "pre_setup_output_build_matrix.json", JsonSerializer.Serialize(outputBuildMatrix, JsonExtension.SnakeCaseNamingOption));
        File.WriteAllText(Nuke.Common.NukeBuild.TemporaryDirectory / "pre_setup_output_publish_matrix.json", JsonSerializer.Serialize(outputPublishMatrix, JsonExtension.SnakeCaseNamingOption));
        Log.Information("NUKE_PRE_SETUP_OUTPUT_TEST_MATRIX: {outputMatrix}", JsonSerializer.Serialize(outputTestMatrix, JsonExtension.SnakeCaseNamingOptionIndented));
        Log.Information("NUKE_PRE_SETUP_OUTPUT_BUILD_MATRIX: {outputMatrix}", JsonSerializer.Serialize(outputBuildMatrix, JsonExtension.SnakeCaseNamingOptionIndented));
        Log.Information("NUKE_PRE_SETUP_OUTPUT_PUBLISH_MATRIX: {outputMatrix}", JsonSerializer.Serialize(outputPublishMatrix, JsonExtension.SnakeCaseNamingOptionIndented));
    }

    public void BuildWorkflow()
    {
        ValueHelpers.GetOrFail(AppEntryHelpers.GetAppConfig, out var appConfig);

        List<WorkflowBuilder> workflowBuilders = [.. ClassHelpers.GetInstances<WorkflowBuilder>().OrderByDescending(i => i.Priority)];

        NukeBuild.SetupWorkflowBuilder(workflowBuilders, PipelineType.Azure);

        var appEntrySecretMap = AppEntryHelpers.GetEntrySecretMap<AppEntry>();
        var appTestEntrySecretMap = AppEntryHelpers.GetEntrySecretMap<AppTestEntry>();

        Dictionary<string, object> workflow = new()
        {
            ["name"] = "Nuke CICD Pipeline",
            ["trigger"] = new Dictionary<string, object>()
                {
                    { "batch", true },
                    { "branches", new Dictionary<string, object>()
                        {
                            { "include", NukeBuild.EnvironmentBranches.ToArray() },
                        }
                    },
                    { "tags", new Dictionary<string, object>()
                        {
                            { "include", new List<string> { "bump-*" } }
                        }
                    }
                },
            ["pr"] = new Dictionary<string, object>()
                {
                    { "branches", new Dictionary<string, object>()
                        {
                            { "include", new List<string> { "**" } },
                        }
                    }
                },
            ["jobs"] = new List<object>()
        };

        List<string> needs = [];

        // ██████████████████████████████████████
        // ██████████████ Pre Setup █████████████
        // ██████████████████████████████████████
        var preSetupJob = AddJob(workflow, "pre_setup", "Pre Setup", RunnerOS.Ubuntu2204);
        AddJobStepCheckout(preSetupJob, fetchDepth: 0);
        var nukePreSetupStep = AddJobStepNukeRun(preSetupJob, RunnerOS.Ubuntu2204, "PipelinePreSetup", "azure");
        AddStepEnvVar(nukePreSetupStep, "GITHUB_TOKEN", "$(GITHUB_TOKEN)");
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
        var testJob = AddJob(workflow, "test", "Test", "$(nuke_pool_name)", "$(nuke_pool_vm_image)", needs: [.. needs], condition: "succeeded()");
        AddJobEnvVarFromNeeds(testJob, "NUKE_PRE_SETUP_OUTPUT", "pre_setup");
        AddJobMatrixIncludeFromPreSetup(testJob, "NUKE_PRE_SETUP_OUTPUT_TEST_MATRIX");
        AddJobStepCheckout(testJob, condition: "ne(variables['nuke_entry_id'], 'skip')");
        AddJobStepsFromBuilder(testJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPreTestRun(step));
        var cacheTestStep = AddJobStep(testJob, displayName: "Cache Test", task: "Cache@2", condition: "ne(variables['nuke_entry_id'], 'skip')");
        AddJobStepInputs(cacheTestStep, "path", "./.nuke/cache");
        AddJobStepInputs(cacheTestStep, "key", $"""
            "test" | "$(nuke_runner_name)" | "$(nuke_entry_id)" | "$(nuke_cache_invalidator)" | "$(nuke_environment)" | "$(nuke_run_classification)" | "$(nuke_run_identifier)"
            """);
        AddJobStepInputs(cacheTestStep, "restoreKeys", $"""
            "test" | "$(nuke_runner_name)" | "$(nuke_entry_id)" | "$(nuke_cache_invalidator)" | "$(nuke_environment)" | "$(nuke_run_classification)"
            "test" | "$(nuke_runner_name)" | "$(nuke_entry_id)" | "$(nuke_cache_invalidator)" | "$(nuke_environment)" | "main"
            """);
        var nukeTestStep = AddJobStepNukeRun(testJob, "$(run_script)", "PipelineTest", "$(entry_ids_to_run)", condition: "ne(variables['nuke_entry_id'], 'skip')");
        AddJobStepsFromBuilder(testJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPostTestRun(step));
        AddStepEnvVarFromSecretMap(nukeTestStep, appTestEntrySecretMap);
        needs.Add("test");

        // ██████████████████████████████████████
        // ███████████████ Build ████████████████
        // ██████████████████████████████████████
        var buildJob = AddJob(workflow, "build", "Build", "$(nuke_pool_name)", "$(nuke_pool_vm_image)", needs: [.. needs], condition: "succeeded()");
        AddJobMatrixIncludeFromPreSetup(buildJob, "NUKE_PRE_SETUP_OUTPUT_BUILD_MATRIX");
        AddJobEnvVarFromNeeds(buildJob, "NUKE_PRE_SETUP_OUTPUT", "pre_setup");
        AddJobStepCheckout(buildJob, condition: "ne(variables['nuke_entry_id'], 'skip')");
        AddJobStepsFromBuilder(buildJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPreBuildRun(step));
        var cacheBuildStep = AddJobStep(buildJob, displayName: "Cache Build", task: "Cache@2", condition: "ne(variables['nuke_entry_id'], 'skip')");
        AddJobStepInputs(cacheBuildStep, "path", "./.nuke/cache");
        AddJobStepInputs(cacheBuildStep, "key", $"""
            "build" | "$(nuke_runner_name)" | "$(nuke_entry_id)" | "$(nuke_cache_invalidator)" | "$(nuke_environment)" | "$(nuke_run_classification)" | "$(nuke_run_identifier)"
            """);
        AddJobStepInputs(cacheBuildStep, "restoreKeys", $"""
            "build" | "$(nuke_runner_name)" | "$(nuke_entry_id)" | "$(nuke_cache_invalidator)" | "$(nuke_environment)" | "$(nuke_run_classification)"
            "build" | "$(nuke_runner_name)" | "$(nuke_entry_id)" | "$(nuke_cache_invalidator)" | "$(nuke_environment)" | "main"
            """);
        var nukeBuildStep = AddJobStepNukeRun(buildJob, "$(nuke_run_script)", "PipelineBuild", "$(nuke_entry_ids_to_run)", condition: "ne(variables['nuke_entry_id'], 'skip')");
        AddStepEnvVarFromSecretMap(nukeBuildStep, appEntrySecretMap);
        AddJobStepsFromBuilder(buildJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPostBuildRun(step));
        var uploadBuildStep = AddJobStep(buildJob, displayName: "Upload Artifacts", task: "PublishPipelineArtifact@1", condition: "ne(variables['nuke_entry_id'], 'skip')");
        AddJobStepInputs(uploadBuildStep, "artifact", "$(nuke_entry_id)");
        AddJobStepInputs(uploadBuildStep, "targetPath", "./.nuke/output");
        AddJobStepInputs(uploadBuildStep, "continueOnError", "true");
        needs.Add("build");

        // ██████████████████████████████████████
        // ██████████████ Publish ███████████████
        // ██████████████████████████████████████
        var publishJob = AddJob(workflow, "publish", "Publish", "$(nuke_pool_name)", "$(nuke_pool_vm_image)", needs: [.. needs], condition: "succeeded()");
        AddJobEnvVarFromNeeds(publishJob, "NUKE_PRE_SETUP_OUTPUT", "pre_setup");
        AddJobMatrixIncludeFromPreSetup(publishJob, "NUKE_PRE_SETUP_OUTPUT_PUBLISH_MATRIX");
        AddJobStepCheckout(publishJob, condition: "ne(variables['nuke_entry_id'], 'skip')");
        var downloadPublishStep = AddJobStep(publishJob, displayName: "Download Artifacts", task: "DownloadPipelineArtifact@2", condition: "ne(variables['nuke_entry_id'], 'skip')");
        AddJobStepInputs(downloadPublishStep, "artifact", "$(nuke_entry_id)");
        AddJobStepInputs(downloadPublishStep, "path", "./.nuke/output");
        AddJobStepInputs(downloadPublishStep, "continueOnError", "true");
        AddJobStepsFromBuilder(publishJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPrePublishRun(step));
        var cachePublishStep = AddJobStep(publishJob, displayName: "Cache Publish", task: "Cache@2", condition: "ne(variables['nuke_entry_id'], 'skip')");
        AddJobStepInputs(cachePublishStep, "path", "./.nuke/cache");
        AddJobStepInputs(cachePublishStep, "key", $"""
            "publish" | "$(nuke_runner_name)" | "$(nuke_entry_id)" | "$(nuke_cache_invalidator)" | "$(nuke_environment)" | "$(nuke_run_classification)" | "$(nuke_run_identifier)"
            """);
        AddJobStepInputs(cachePublishStep, "restoreKeys", $"""
            "publish" | "$(nuke_runner_name)" | "$(nuke_entry_id)" | "$(nuke_cache_invalidator)" | "$(nuke_environment)" | "$(nuke_run_classification)"
            "publish" | "$(nuke_runner_name)" | "$(nuke_entry_id)" | "$(nuke_cache_invalidator)" | "$(nuke_environment)" | "main"
            """);
        var nukePublishStep = AddJobStepNukeRun(publishJob, "$(nuke_run_script)", "PipelinePublish", "$(nuke_entry_ids_to_run)", condition: "ne(variables['nuke_entry_id'], 'skip')");
        AddStepEnvVarFromSecretMap(nukePublishStep, appEntrySecretMap);
        AddJobStepsFromBuilder(publishJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPostPublishRun(step));
        needs.Add("publish");

        // ██████████████████████████████████████
        // ███████████ Run Validation ███████████
        // ██████████████████████████████████████
        var runValidationJob = AddJob(workflow, "run_validation", "Run Validation", RunnerOS.Ubuntu2204, needs: [.. needs], condition: "succeeded()");
        AddJobEnvVar(runValidationJob, "NUKE_RUN_SUCCESS_AZURE", "$[ dependencies.publish.result ]");
        AddJobStep(runValidationJob, displayName: $"Resolve NUKE_RUN_SUCCESS",
            script: $"echo \"##vso[task.setvariable variable=NUKE_RUN_SUCCESS]${{NUKE_RUN_SUCCESS_AZURE/Succeeded/ok}}\"");
        AddJobStep(runValidationJob, displayName: $"Output NUKE_RUN_SUCCESS",
            script: $"echo \"##vso[task.setvariable variable=NUKE_RUN_SUCCESS]$NUKE_RUN_SUCCESS\" && echo \"##vso[task.setvariable variable=NUKE_RUN_SUCCESS;isOutput=true]$NUKE_RUN_SUCCESS\"");
        needs.Add("run_validation");

        // ██████████████████████████████████████
        // █████████████ Post Setup █████████████
        // ██████████████████████████████████████
        var postSetupJob = AddJob(workflow, "post_setup", $"Post Setup", RunnerOS.Ubuntu2204, needs: [.. needs], condition: "always()");
        AddJobEnvVarFromNeeds(postSetupJob, "NUKE_PRE_SETUP_OUTPUT", "pre_setup");
        AddJobEnvVar(postSetupJob, "NUKE_PUBLISH_SUCCESS_AZURE", "$[ dependencies.publish.result ]");
        AddJobStep(postSetupJob, name: "NUKE_PUBLISH_SUCCESS", displayName: $"Resolve NUKE_PUBLISH_SUCCESS",
            script: $"echo \"##vso[task.setvariable variable=NUKE_PUBLISH_SUCCESS]${{NUKE_PUBLISH_SUCCESS_AZURE/Succeeded/ok}}\"");
        AddJobStepCheckout(postSetupJob);
        var downloadPostSetupStep = AddJobStep(postSetupJob, displayName: "Download artifacts", task: "DownloadPipelineArtifact@2");
        AddJobStepInputs(downloadPostSetupStep, "path", "./.nuke/output");
        AddJobStepInputs(downloadPostSetupStep, "patterns", "**");
        AddJobStepInputs(downloadPostSetupStep, "continueOnError", "true");
        var nukePostSetupStep = AddJobStepNukeRun(postSetupJob, RunnerOS.Ubuntu2204, "PipelinePostSetup");
        AddStepEnvVar(nukePostSetupStep, "GITHUB_TOKEN", "$(GITHUB_TOKEN)");
        
        // ██████████████████████████████████████
        // ███████████████ Write ████████████████
        // ██████████████████████████████████████
        var workflowDirPath = Nuke.Common.NukeBuild.RootDirectory;
        var workflowPath = Nuke.Common.NukeBuild.RootDirectory / "azure-pipelines.yml";

        Directory.CreateDirectory(workflowDirPath);
        File.WriteAllText(workflowPath, YamlExtension.Serialize(workflow));

        Log.Information("Workflow built at " + workflowPath.ToString());
    }

    private static void AddJobMatrixInclude(Dictionary<string, object> job, string matrixInclude)
    {
        if (!job.TryGetValue("strategy", out object? value))
        {
            value = new Dictionary<string, object>();
            job["strategy"] = value;
        }
        ((Dictionary<string, object>)value)["matrix"] = matrixInclude;
    }

    private static void AddJobMatrixIncludeFromPreSetup(Dictionary<string, object> job, string outputName)
    {
        AddJobMatrixInclude(job, $"$[ dependencies.pre_setup.outputs['{outputName}.{outputName}'] ]");
    }

    private static Dictionary<string, object> AddJob(Dictionary<string, object> workflow, string id, string name, string? poolName, string? poolVMImage, IEnumerable<string>? needs = null, string condition = "")
    {
        Dictionary<string, object> job = new()
        {
            ["job"] = id,
            ["displayName"] = name,
            ["pool"] = new Dictionary<string, object?>
            {
                { "name", poolName },
                { "vmImage", poolVMImage }
            },
            ["steps"] = new List<object>()
        };
        if (needs != null && needs.Any())
        {
            job["dependsOn"] = needs;
        }
        if (!string.IsNullOrEmpty(condition))
        {
            job["condition"] = condition;
        }
        ((List<object>)workflow["jobs"]).Add(job);
        return job;
    }

    private static Dictionary<string, object> AddJob(Dictionary<string, object> workflow, string id, string name, RunnerOS runnerOS, IEnumerable<string>? needs = null, string condition = "")
    {
        var azureRunnerOs = (runnerOS.GetPipelineOS(PipelineType.Azure) as RunnerAzurePipelineOS)!;
        return AddJob(workflow, id, name, azureRunnerOs.PoolName, azureRunnerOs.PoolVMImage, needs, condition);
    }

    private static Dictionary<string, object> AddJobStep(Dictionary<string, object> job, string name = "", string displayName = "", string task = "", string script = "", string condition = "")
    {
        Dictionary<string, object> step = [];
        ((List<object>)job["steps"]).Add(step);
        if (!string.IsNullOrEmpty(script))
        {
            step["script"] = script;
        }
        if (!string.IsNullOrEmpty(task))
        {
            step["task"] = task;
        }
        if (!string.IsNullOrEmpty(name))
        {
            step["name"] = name;
        }
        if (!string.IsNullOrEmpty(displayName))
        {
            step["displayName"] = displayName;
        }
        if (!string.IsNullOrEmpty(condition))
        {
            step["condition"] = condition;
        }
        return step;
    }

    private static void AddJobStepsFromBuilder(Dictionary<string, object> job, List<WorkflowBuilder> workflowBuilders, Action<WorkflowBuilder, Dictionary<string, object>> toBuild)
    {
        foreach (var workflowBuilder in workflowBuilders)
        {
            Dictionary<string, object> step = [];
            toBuild.Invoke(workflowBuilder, step);
            if (step.Count > 0)
            {
                ((List<object>)job["steps"]).Add(step);
            }
        }
    }

    private static Dictionary<string, object> AddJobStepCheckout(Dictionary<string, object> job, string condition = "", int? fetchDepth = null)
    {
        Dictionary<string, object> step = AddJobStep(job);
        step["checkout"] = "self";
        step["persistCredentials"] = "true";
        if (!string.IsNullOrEmpty(condition))
        {
            step["condition"] = condition;
        }
        if (fetchDepth != null)
        {
            step["fetchDepth"] = fetchDepth.Value.ToString();
        }
        return step;
    }

    private static Dictionary<string, object> AddJobStepNukeRun(Dictionary<string, object> job, string buildScript, string targetName, string args = "", string condition = "")
    {
        var script = $"{buildScript} {targetName}";
        if (!string.IsNullOrEmpty(args))
        {
            script += $" --args \"{args}\"";
        }
        return AddJobStep(job, displayName: $"Run Nuke {targetName}", script: script, condition: condition);
    }

    private static Dictionary<string, object> AddJobStepNukeRun(Dictionary<string, object> job, RunnerOS runnerOS, string targetName, string args = "", string condition = "")
    {
        return AddJobStepNukeRun(job, runnerOS.GetRunScript(PipelineType.Azure), targetName, args, condition);
    }

    private static void AddJobStepInputs(Dictionary<string, object> step, string name, string value)
    {
        if (!step.TryGetValue("inputs", out object? withValue))
        {
            withValue = new Dictionary<string, object>();
            step["inputs"] = withValue;
        }
        ((Dictionary<string, object>)withValue)[name] = value;
    }

    private static void AddJobEnvVar(Dictionary<string, object> job, string envVarName, string envVarValue)
    {
        if (!job.TryGetValue("variables", out object? value))
        {
            value = new Dictionary<string, object>();
            job["variables"] = value;
        }
        ((Dictionary<string, object>)value)[envVarName] = envVarValue;
    }

    private static void AddStepEnvVar(Dictionary<string, object> step, string envVarName, string envVarValue)
    {
        if (!step.TryGetValue("env", out object? value))
        {
            value = new Dictionary<string, object>();
            step["env"] = value;
        }
        ((Dictionary<string, object>)value)[envVarName] = envVarValue;
    }

    private static void AddJobEnvVarFromNeeds(Dictionary<string, object> job, string envVarName, string needsId)
    {
        AddJobEnvVar(job, envVarName, $"$[ dependencies.{needsId}.outputs['{envVarName}.{envVarName}'] ]");
    }

    private static void AddStepEnvVarFromSecretMap(Dictionary<string, object> step, Dictionary<string, (Type EntryType, List<(MemberInfo MemberInfo, Attributes.SecretVariableAttribute Secret)> Secrets)> secretMap)
    {
        foreach (var map in secretMap)
        {
            foreach (var secret in map.Value.Secrets)
            {
                var envVarName = string.IsNullOrEmpty(secret.Secret.EnvironmentVariableName) ? $"NUKE_{secret.Secret.SecretVariableName}" : secret.Secret.EnvironmentVariableName;
                AddStepEnvVar(step, envVarName, $"$({secret.Secret.SecretVariableName})");
            }
        }
    }

    private static void AddJobOutputFromFile(Dictionary<string, object> job, string envVarName, string filename)
    {
        AddJobStep(job, name: envVarName, displayName: $"Output {envVarName}",
            script: $"echo \"##vso[task.setvariable variable={envVarName}]$(cat {filename})\" && echo \"##vso[task.setvariable variable={envVarName};isOutput=true]$(cat {filename})\"");
    }
}
