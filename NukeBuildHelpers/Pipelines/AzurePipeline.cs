using Nuke.Common;
using Nuke.Common.IO;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Models;
using NukeBuildHelpers.Pipelines.Interfaces;
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
        var outputTestMatrix = new Dictionary<string, PreSetupOutputAppTestEntryMatrix>();
        var outputBuildMatrix = new Dictionary<string, PreSetupOutputAppEntryMatrix>();
        var outputPublishMatrix = new Dictionary<string, PreSetupOutputAppEntryMatrix>();

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
            outputTestMatrix.Add(appTestEntry.Id, new()
            {
                Id = appTestEntry.Id,
                Name = appTestEntry.Name,
                Environment = preSetupOutput.Environment,
                RunsOn = GetRunsOn(appTestEntry.RunsOn),
                BuildScript = GetBuildScript(appTestEntry.RunsOn),
                IdsToRun = $"{appEntry.Id};{appTestEntry.Id}",
                CacheInvalidator = appEntry.CacheInvalidator,
                RunClassification = runClassification,
                RunIdentifier = runIdentifier
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
            outputBuildMatrix.Add(appEntry.Id, new()
            {
                Id = appEntry.Id,
                Name = appEntry.Name,
                Environment = preSetupOutput.Environment,
                RunsOn = GetRunsOn(appEntry.BuildRunsOn),
                BuildScript = GetBuildScript(appEntry.BuildRunsOn),
                IdsToRun = appEntry.Id,
                Version = entry.Version.ToString(),
                CacheInvalidator = entry.AppEntry.CacheInvalidator,
                RunClassification = runClassification,
                RunIdentifier = runIdentifier
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
            outputPublishMatrix.Add(appEntry.Id, new()
            {
                Id = appEntry.Id,
                Name = appEntry.Name,
                Environment = preSetupOutput.Environment,
                RunsOn = GetRunsOn(appEntry.BuildRunsOn),
                BuildScript = GetBuildScript(appEntry.BuildRunsOn),
                IdsToRun = appEntry.Id,
                Version = entry.Version.ToString(),
                CacheInvalidator = entry.AppEntry.CacheInvalidator,
                RunClassification = runClassification,
                RunIdentifier = runIdentifier
            });
        }

        if (outputTestMatrix.Count == 0)
        {
            outputTestMatrix.Add("skip", new()
            {
                Id = "skip",
                Name = "Skip",
                Environment = preSetupOutput.Environment,
                RunsOn = GetRunsOn(RunsOnType.Ubuntu2204),
                BuildScript = "",
                IdsToRun = "",
                CacheInvalidator = "",
                RunClassification = "",
                RunIdentifier = ""
            });
        }
        if (outputBuildMatrix.Count == 0)
        {
            outputBuildMatrix.Add("skip", new()
            {
                Id = "skip",
                Name = "Skip",
                Environment = preSetupOutput.Environment,
                RunsOn = GetRunsOn(RunsOnType.Ubuntu2204),
                BuildScript = "",
                IdsToRun = "",
                Version = "",
                CacheInvalidator = "",
                RunClassification = "",
                RunIdentifier = ""
            });
        }
        if (outputPublishMatrix.Count == 0)
        {
            outputPublishMatrix.Add("skip", new()
            {
                Id = "skip",
                Name = "Skip",
                Environment = preSetupOutput.Environment,
                RunsOn = GetRunsOn(RunsOnType.Ubuntu2204),
                BuildScript = "",
                IdsToRun = "",
                Version = "",
                CacheInvalidator = "",
                RunClassification = "",
                RunIdentifier = ""
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
        var preSetupJob = AddJob(workflow, "pre_setup", "Pre Setup", RunsOnType.Ubuntu2204);
        AddJobStepCheckout(preSetupJob, fetchDepth: 0);
        var nukePreSetupStep = AddJobStepNukeRun(preSetupJob, RunsOnType.Ubuntu2204, "PipelinePreSetup", "azure");
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
        var testJob = AddJob(workflow, "test", "Test", "$(runs_on)", needs: [.. needs], condition: "succeeded()");
        AddJobEnvVarFromNeeds(testJob, "NUKE_PRE_SETUP_OUTPUT", "pre_setup");
        AddJobMatrixIncludeFromPreSetup(testJob, "NUKE_PRE_SETUP_OUTPUT_TEST_MATRIX");
        AddJobStepCheckout(testJob, condition: "ne(variables['id'], 'skip')");
        AddJobStepsFromBuilder(testJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPreTestRun(step));
        var cacheTestStep = AddJobStep(testJob, displayName: "Cache Test", task: "Cache@2", condition: "ne(variables['id'], 'skip')");
        AddJobStepInputs(cacheTestStep, "path", "./.nuke/cache");
        AddJobStepInputs(cacheTestStep, "key", $"""
            "test" | "$(runs_on)" | "$(id)" | "$(cache_invalidator)" | "$(environment)" | "$(run_classification)" | "$(run_identifier)"
            """);
        AddJobStepInputs(cacheTestStep, "restoreKeys", $"""
            "test" | "$(runs_on)" | "$(id)" | "$(cache_invalidator)" | "$(environment)" | "$(run_classification)"
            "test" | "$(runs_on)" | "$(id)" | "$(cache_invalidator)" | "$(environment)" | "main"
            """);
        var nukeTestStep = AddJobStepNukeRun(testJob, "$(build_script)", "PipelineTest", "$(ids_to_run)", condition: "ne(variables['id'], 'skip')");
        AddJobStepsFromBuilder(testJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPostTestRun(step));
        AddStepEnvVarFromSecretMap(nukeTestStep, appTestEntrySecretMap);
        needs.Add("test");

        // ██████████████████████████████████████
        // ███████████ Test Validation ██████████
        // ██████████████████████████████████████
        var testValidationJob = AddJob(workflow, "test_validation", "Test Validation", RunsOnType.Ubuntu2204, needs: [.. needs], condition: "succeeded()");
        AddJobEnvVar(testValidationJob, "NUKE_TEST_SUCCESS_AZURE", "$[ dependencies.test.result ]");
        AddJobStep(testValidationJob, displayName: $"Resolve NUKE_TEST_SUCCESS",
            script: $"echo \"##vso[task.setvariable variable=NUKE_TEST_SUCCESS]${{NUKE_TEST_SUCCESS_AZURE/Succeeded/ok}}\"");
        AddJobStep(testValidationJob, displayName: $"Output NUKE_TEST_SUCCESS",
            script: $"echo \"##vso[task.setvariable variable=NUKE_TEST_SUCCESS]$NUKE_TEST_SUCCESS\" && echo \"##vso[task.setvariable variable=NUKE_TEST_SUCCESS;isOutput=true]$NUKE_TEST_SUCCESS\"");
        needs.Add("test_validation");

        // ██████████████████████████████████████
        // ███████████████ Build ████████████████
        // ██████████████████████████████████████
        var buildJob = AddJob(workflow, "build", "Build", "$(runs_on)", needs: [.. needs], condition: "succeeded()");
        AddJobMatrixIncludeFromPreSetup(buildJob, "NUKE_PRE_SETUP_OUTPUT_BUILD_MATRIX");
        AddJobEnvVarFromNeeds(buildJob, "NUKE_PRE_SETUP_OUTPUT", "pre_setup");
        AddJobStepCheckout(buildJob, condition: "ne(variables['id'], 'skip')");
        AddJobStepsFromBuilder(buildJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPreBuildRun(step));
        var cacheBuildStep = AddJobStep(buildJob, displayName: "Cache Build", task: "Cache@2", condition: "ne(variables['id'], 'skip')");
        AddJobStepInputs(cacheBuildStep, "path", "./.nuke/cache");
        AddJobStepInputs(cacheBuildStep, "key", $"""
            "build" | "$(runs_on)" | "$(id)" | "$(cache_invalidator)" | "$(environment)" | "$(run_classification)" | "$(run_identifier)"
            """);
        AddJobStepInputs(cacheBuildStep, "restoreKeys", $"""
            "build" | "$(runs_on)" | "$(id)" | "$(cache_invalidator)" | "$(environment)" | "$(run_classification)"
            "build" | "$(runs_on)" | "$(id)" | "$(cache_invalidator)" | "$(environment)" | "main"
            """);
        var nukeBuildStep = AddJobStepNukeRun(buildJob, "$(build_script)", "PipelineBuild", "$(ids_to_run)", condition: "ne(variables['id'], 'skip')");
        AddStepEnvVarFromSecretMap(nukeBuildStep, appEntrySecretMap);
        AddJobStepsFromBuilder(buildJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPostBuildRun(step));
        var uploadBuildStep = AddJobStep(buildJob, displayName: "Upload Artifacts", task: "PublishPipelineArtifact@1", condition: "ne(variables['id'], 'skip')");
        AddJobStepInputs(uploadBuildStep, "artifact", "$(id)");
        AddJobStepInputs(uploadBuildStep, "targetPath", "./.nuke/output");
        AddJobStepInputs(uploadBuildStep, "continueOnError", "true");
        needs.Add("build");

        // ██████████████████████████████████████
        // ██████████ Build Validation ██████████
        // ██████████████████████████████████████
        var buildValidationJob = AddJob(workflow, "build_validation", "Build Validation", RunsOnType.Ubuntu2204, needs: [.. needs], condition: "succeeded()");
        AddJobEnvVar(buildValidationJob, "NUKE_BUILD_SUCCESS_AZURE", "$[ dependencies.build.result ]");
        AddJobStep(buildValidationJob, displayName: $"Resolve NUKE_BUILD_SUCCESS",
            script: $"echo \"##vso[task.setvariable variable=NUKE_BUILD_SUCCESS]${{NUKE_BUILD_SUCCESS_AZURE/Succeeded/ok}}\"");
        AddJobStep(buildValidationJob, displayName: $"Output NUKE_BUILD_SUCCESS",
            script: $"echo \"##vso[task.setvariable variable=NUKE_BUILD_SUCCESS]$NUKE_BUILD_SUCCESS\" && echo \"##vso[task.setvariable variable=NUKE_BUILD_SUCCESS;isOutput=true]$NUKE_BUILD_SUCCESS\"");
        needs.Add("build_validation");

        // ██████████████████████████████████████
        // ██████████████ Publish ███████████████
        // ██████████████████████████████████████
        var publishJob = AddJob(workflow, "publish", "Publish", "$(runs_on)", needs: [.. needs], condition: "succeeded()");
        AddJobEnvVarFromNeeds(publishJob, "NUKE_PRE_SETUP_OUTPUT", "pre_setup");
        AddJobMatrixIncludeFromPreSetup(publishJob, "NUKE_PRE_SETUP_OUTPUT_PUBLISH_MATRIX");
        AddJobStepCheckout(publishJob, condition: "ne(variables['id'], 'skip')");
        var downloadPublishStep = AddJobStep(publishJob, displayName: "Download Artifacts", task: "DownloadPipelineArtifact@2", condition: "ne(variables['id'], 'skip')");
        AddJobStepInputs(downloadPublishStep, "artifact", "$(id)");
        AddJobStepInputs(downloadPublishStep, "path", "./.nuke/output");
        AddJobStepInputs(downloadPublishStep, "continueOnError", "true");
        AddJobStepsFromBuilder(publishJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPrePublishRun(step));
        var cachePublishStep = AddJobStep(publishJob, displayName: "Cache Publish", task: "Cache@2", condition: "ne(variables['id'], 'skip')");
        AddJobStepInputs(cachePublishStep, "path", "./.nuke/cache");
        AddJobStepInputs(cachePublishStep, "key", $"""
            "publish" | "$(runs_on)" | "$(id)" | "$(cache_invalidator)" | "$(environment)" | "$(run_classification)" | "$(run_identifier)"
            """);
        AddJobStepInputs(cachePublishStep, "restoreKeys", $"""
            "publish" | "$(runs_on)" | "$(id)" | "$(cache_invalidator)" | "$(environment)" | "$(run_classification)"
            "publish" | "$(runs_on)" | "$(id)" | "$(cache_invalidator)" | "$(environment)" | "main"
            """);
        var nukePublishStep = AddJobStepNukeRun(publishJob, "$(build_script)", "PipelinePublish", "$(ids_to_run)", condition: "ne(variables['id'], 'skip')");
        AddStepEnvVarFromSecretMap(nukePublishStep, appEntrySecretMap);
        AddJobStepsFromBuilder(publishJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPostPublishRun(step));
        needs.Add("publish");

        // ██████████████████████████████████████
        // █████████ Publish Validation █████████
        // ██████████████████████████████████████
        var publishValidationJob = AddJob(workflow, "publish_validation", "Publish Validation", RunsOnType.Ubuntu2204, needs: [.. needs], condition: "succeeded()");
        AddJobEnvVar(publishValidationJob, "NUKE_PUBLISH_SUCCESS_AZURE", "$[ dependencies.publish.result ]");
        AddJobStep(publishValidationJob, displayName: $"Resolve NUKE_PUBLISH_SUCCESS",
            script: $"echo \"##vso[task.setvariable variable=NUKE_PUBLISH_SUCCESS]${{NUKE_PUBLISH_SUCCESS_AZURE/Succeeded/ok}}\"");
        AddJobStep(publishValidationJob, displayName: $"Output NUKE_PUBLISH_SUCCESS",
            script: $"echo \"##vso[task.setvariable variable=NUKE_PUBLISH_SUCCESS]$NUKE_PUBLISH_SUCCESS\" && echo \"##vso[task.setvariable variable=NUKE_PUBLISH_SUCCESS;isOutput=true]$NUKE_PUBLISH_SUCCESS\"");
        needs.Add("publish_validation");

        // ██████████████████████████████████████
        // █████████████ Post Setup █████████████
        // ██████████████████████████████████████
        var postSetupJob = AddJob(workflow, "post_setup", $"Post Setup", RunsOnType.Ubuntu2204, needs: [.. needs], condition: "always()");
        AddJobEnvVarFromNeeds(postSetupJob, "NUKE_PRE_SETUP_OUTPUT", "pre_setup");
        AddJobEnvVar(postSetupJob, "NUKE_PUBLISH_SUCCESS_AZURE", "$[ dependencies.publish.result ]");
        AddJobStep(postSetupJob, name: "NUKE_PUBLISH_SUCCESS", displayName: $"Resolve NUKE_PUBLISH_SUCCESS",
            script: $"echo \"##vso[task.setvariable variable=NUKE_PUBLISH_SUCCESS]${{NUKE_PUBLISH_SUCCESS_AZURE/Succeeded/ok}}\"");
        AddJobStepCheckout(postSetupJob);
        var downloadPostSetupStep = AddJobStep(postSetupJob, displayName: "Download artifacts", task: "DownloadPipelineArtifact@2");
        AddJobStepInputs(downloadPostSetupStep, "path", "./.nuke/output");
        AddJobStepInputs(downloadPostSetupStep, "patterns", "**");
        AddJobStepInputs(downloadPostSetupStep, "continueOnError", "true");
        var nukePostSetupStep = AddJobStepNukeRun(postSetupJob, GetBuildScript(RunsOnType.Ubuntu2204), "PipelinePostSetup");
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

    private static string GetRunsOn(RunsOnType runsOnType)
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

    private static string GetBuildScript(RunsOnType runsOnType)
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

    private static Dictionary<string, object> AddJob(Dictionary<string, object> workflow, string id, string name, string runsOn, IEnumerable<string>? needs = null, string condition = "")
    {
        Dictionary<string, object> job = new()
        {
            ["job"] = id,
            ["displayName"] = name,
            ["pool"] = new Dictionary<string, object>
            {
                { "vmImage", runsOn }
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

    private static Dictionary<string, object> AddJob(Dictionary<string, object> workflow, string id, string name, RunsOnType buildsOnType, IEnumerable<string>? needs = null, string condition = "")
    {
        return AddJob(workflow, id, name, GetRunsOn(buildsOnType), needs, condition);
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

    private static Dictionary<string, object> AddJobStepNukeRun(Dictionary<string, object> job, RunsOnType runsOnType, string targetName, string args = "", string condition = "")
    {
        return AddJobStepNukeRun(job, GetBuildScript(runsOnType), targetName, args, condition);
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
