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

internal class GithubPipeline(BaseNukeBuildHelpers nukeBuild) : IPipeline
{
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

    public void Prepare(PreSetupOutput preSetupOutput, AppConfig appConfig, Dictionary<string, AppRunEntry> toEntry)
    {
        var outputTestMatrix = new List<PreSetupOutputAppTestEntryMatrix>();
        var outputBuildMatrix = new List<PreSetupOutputAppEntryMatrix>();
        var outputPublishMatrix = new List<PreSetupOutputAppEntryMatrix>();

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
            outputTestMatrix.Add(new()
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
            outputBuildMatrix.Add(new()
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
            outputPublishMatrix.Add(new()
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
            outputTestMatrix.Add(new()
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
            outputBuildMatrix.Add(new()
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
            outputPublishMatrix.Add(new()
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

        NukeBuild.SetupWorkflowBuilder(workflowBuilders, PipelineType.Github);

        var appEntrySecretMap = AppEntryHelpers.GetEntrySecretMap<AppEntry>();
        var appTestEntrySecretMap = AppEntryHelpers.GetEntrySecretMap<AppTestEntry>();

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
            ["jobs"] = new Dictionary<string, object>()
        };

        List<string> needs = [];

        // ██████████████████████████████████████
        // ██████████████ Pre Setup █████████████
        // ██████████████████████████████████████
        var preSetupJob = AddJob(workflow, "pre_setup", "Pre Setup", RunsOnType.Ubuntu2204);
        AddJobOrStepEnvVar(preSetupJob, "GITHUB_TOKEN", "${{ secrets.GITHUB_TOKEN }}");
        AddJobStepCheckout(preSetupJob, fetchDepth: 0);
        AddJobStepNukeRun(preSetupJob, RunsOnType.Ubuntu2204, "PipelinePreSetup", "github");
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
        var testJob = AddJob(workflow, "test", "Test - ${{ matrix.name }}", "${{ matrix.runs_on }}", needs: [.. needs], _if: "success()");
        AddJobOrStepEnvVarFromNeeds(testJob, "NUKE_PRE_SETUP_OUTPUT", "pre_setup");
        AddJobMatrixIncludeFromPreSetup(testJob, "NUKE_PRE_SETUP_OUTPUT_TEST_MATRIX");
        AddJobStepCheckout(testJob, _if: "${{ matrix.id != 'skip' }}");
        AddJobStepsFromBuilder(testJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPreTestRun(step));
        var cacheTestStep = AddJobStep(testJob, name: "Cache Test", uses: "actions/cache@v4", _if: "${{ matrix.id != 'skip' }}");
        AddJobStepWith(cacheTestStep, "path", "./.nuke/cache");
        AddJobStepWith(cacheTestStep, "key", $$$"""
            test-${{ matrix.runs_on }}-${{ matrix.id }}-${{ matrix.cache_invalidator }}-${{ matrix.environment }}-${{ matrix.run_classification }}-${{ matrix.run_identifier }}"
            """);
        AddJobStepWith(cacheTestStep, "restore-keys", $$$"""
            test-${{ matrix.runs_on }}-${{ matrix.id }}-${{ matrix.cache_invalidator }}-${{ matrix.environment }}-${{ matrix.run_classification }}-
            test-${{ matrix.runs_on }}-${{ matrix.id }}-${{ matrix.cache_invalidator }}-${{ matrix.environment }}-main-
            """);
        var nukeTestStep = AddJobStepNukeRun(testJob, "${{ matrix.build_script }}", "PipelineTest", "${{ matrix.ids_to_run }}", _if: "${{ matrix.id != 'skip' }}");
        AddJobStepsFromBuilder(testJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPostTestRun(step));
        AddJobOrStepEnvVarFromSecretMap(nukeTestStep, appTestEntrySecretMap);
        needs.Add("test");

        // ██████████████████████████████████████
        // ███████████ Test Validation ██████████
        // ██████████████████████████████████████
        var testValidationJob = AddJob(workflow, "test_validation", "Test Validation", RunsOnType.Ubuntu2204, _if: "success()");
        needs.Add("test_validation");

        // ██████████████████████████████████████
        // ███████████████ Build ████████████████
        // ██████████████████████████████████████
        var buildJob = AddJob(workflow, "build", "Build - ${{ matrix.name }}", "${{ matrix.runs_on }}", needs: [.. needs], _if: "success()");
        AddJobOrStepEnvVarFromNeeds(buildJob, "NUKE_PRE_SETUP_OUTPUT", "pre_setup");
        AddJobMatrixIncludeFromPreSetup(buildJob, "NUKE_PRE_SETUP_OUTPUT_BUILD_MATRIX");
        AddJobStepCheckout(buildJob, _if: "${{ matrix.id != 'skip' }}");
        AddJobStepsFromBuilder(buildJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPreBuildRun(step));
        var cacheBuildStep = AddJobStep(buildJob, name: "Cache Build", uses: "actions/cache@v4", _if: "${{ matrix.id != 'skip' }}");
        AddJobStepWith(cacheBuildStep, "path", "./.nuke/cache");
        AddJobStepWith(cacheBuildStep, "key", $$$"""
            build-${{ matrix.runs_on }}-${{ matrix.id }}-${{ matrix.cache_invalidator }}-${{ matrix.environment }}-${{ matrix.run_classification }}-${{ matrix.run_identifier }}"
            """);
        AddJobStepWith(cacheBuildStep, "restore-keys", $$$"""
            build-${{ matrix.runs_on }}-${{ matrix.id }}-${{ matrix.cache_invalidator }}-${{ matrix.environment }}-${{ matrix.run_classification }}-
            build-${{ matrix.runs_on }}-${{ matrix.id }}-${{ matrix.cache_invalidator }}-${{ matrix.environment }}-main-
            """);
        var nukeBuild = AddJobStepNukeRun(buildJob, "${{ matrix.build_script }}", "PipelineBuild", "${{ matrix.ids_to_run }}", _if: "${{ matrix.id != 'skip' }}");
        AddJobOrStepEnvVarFromSecretMap(nukeBuild, appEntrySecretMap);
        AddJobStepsFromBuilder(buildJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPostBuildRun(step));
        var uploadBuildStep = AddJobStep(buildJob, name: "Upload Artifacts", uses: "actions/upload-artifact@v4", _if: "${{ matrix.id != 'skip' }}");
        AddJobStepWith(uploadBuildStep, "name", "${{ matrix.id }}");
        AddJobStepWith(uploadBuildStep, "path", "./.nuke/output/*");
        AddJobStepWith(uploadBuildStep, "if-no-files-found", "error");
        AddJobStepWith(uploadBuildStep, "retention-days", "1");
        needs.Add("build");

        // ██████████████████████████████████████
        // ██████████ Build Validation ██████████
        // ██████████████████████████████████████
        var buildValidationJob = AddJob(workflow, "build_validation", "Build Validation", RunsOnType.Ubuntu2204, _if: "success()");
        needs.Add("build_validation");

        // ██████████████████████████████████████
        // ██████████████ Publish ███████████████
        // ██████████████████████████████████████
        var publishJob = AddJob(workflow, "publish", "Publish - ${{ matrix.name }}", "${{ matrix.runs_on }}", needs: [.. needs], _if: "success()");
        AddJobOrStepEnvVarFromNeeds(publishJob, "NUKE_PRE_SETUP_OUTPUT", "pre_setup");
        AddJobMatrixIncludeFromPreSetup(publishJob, "NUKE_PRE_SETUP_OUTPUT_PUBLISH_MATRIX");
        AddJobStepCheckout(publishJob, _if: "${{ matrix.id != 'skip' }}");
        var downloadBuildStep = AddJobStep(publishJob, name: "Download artifacts", uses: "actions/download-artifact@v4", _if: "${{ matrix.id != 'skip' }}");
        AddJobStepWith(downloadBuildStep, "path", "./.nuke/output");
        AddJobStepWith(downloadBuildStep, "pattern", "${{ matrix.id }}");
        AddJobStepWith(downloadBuildStep, "merge-multiple", "true");
        AddJobStepsFromBuilder(publishJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPrePublishRun(step));
        var cachePublishStep = AddJobStep(publishJob, name: "Cache Publish", uses: "actions/cache@v4", _if: "${{ matrix.id != 'skip' }}");
        AddJobStepWith(cachePublishStep, "path", "./.nuke/cache");
        AddJobStepWith(cachePublishStep, "key", $$$"""
            publish-${{ matrix.runs_on }}-${{ matrix.id }}-${{ matrix.cache_invalidator }}-${{ matrix.environment }}-${{ matrix.run_classification }}-${{ matrix.run_identifier }}"
            """);
        AddJobStepWith(cachePublishStep, "restore-keys", $$$"""
            publish-${{ matrix.runs_on }}-${{ matrix.id }}-${{ matrix.cache_invalidator }}-${{ matrix.environment }}-${{ matrix.run_classification }}-
            publish-${{ matrix.runs_on }}-${{ matrix.id }}-${{ matrix.cache_invalidator }}-${{ matrix.environment }}-main-
            """);
        var nukePublishTask = AddJobStepNukeRun(publishJob, "${{ matrix.build_script }}", "PipelinePublish", "${{ matrix.ids_to_run }}", _if: "${{ matrix.id != 'skip' }}");
        AddJobOrStepEnvVarFromSecretMap(nukePublishTask, appEntrySecretMap);
        AddJobStepsFromBuilder(publishJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPostPublishRun(step));
        needs.Add("publish");

        // ██████████████████████████████████████
        // ██████████ Build Validation ██████████
        // ██████████████████████████████████████
        var publishValidationJob = AddJob(workflow, "publish_validation", "Publish Validation", RunsOnType.Ubuntu2204, _if: "success()");
        needs.Add("publish_validation");

        // ██████████████████████████████████████
        // █████████████ Post Setup █████████████
        // ██████████████████████████████████████
        var postSetupJob = AddJob(workflow, "post_setup", $"Post Setup", RunsOnType.Ubuntu2204, needs: [.. needs], _if: "success() || failure() || always()");
        AddJobOrStepEnvVarFromNeeds(postSetupJob, "NUKE_PRE_SETUP_OUTPUT", "pre_setup");
        AddJobOrStepEnvVar(postSetupJob, "NUKE_PUBLISH_SUCCESS_GITHUB", "${{ needs.publish.result }}");
        AddJobStep(postSetupJob, id: "NUKE_PUBLISH_SUCCESS", name: $"Resolve NUKE_PUBLISH_SUCCESS",
            run: $"echo \"NUKE_PUBLISH_SUCCESS=${{NUKE_PUBLISH_SUCCESS_GITHUB/success/ok}}\" >> $GITHUB_OUTPUT");
        AddJobStepCheckout(postSetupJob);
        var downloadPostSetupStep = AddJobStep(postSetupJob, name: "Download Artifacts", uses: "actions/download-artifact@v4");
        AddJobStepWith(downloadPostSetupStep, "path", "./.nuke/output");
        var nukePostSetup = AddJobStepNukeRun(postSetupJob, RunsOnType.Ubuntu2204, "PipelinePostSetup");
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
        return AddJob(workflow, id, name, GetRunsOn(buildsOnType), needs, _if);
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

    private static Dictionary<string, object> AddJobStepNukeRun(Dictionary<string, object> job, RunsOnType runsOnType, string targetName, string args = "", string _if = "")
    {
        return AddJobStepNukeRun(job, GetBuildScript(runsOnType), targetName, args, _if);
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

    private static void AddJobOrStepEnvVarFromSecretMap(Dictionary<string, object> jobOrStep, Dictionary<string, (Type EntryType, List<(MemberInfo MemberInfo, Attributes.SecretVariableAttribute Secret)> Secrets)> secretMap)
    {
        foreach (var map in secretMap)
        {
            foreach (var secret in map.Value.Secrets)
            {
                var envVarName = string.IsNullOrEmpty(secret.Secret.EnvironmentVariableName) ? $"NUKE_{secret.Secret.SecretVariableName}" : secret.Secret.EnvironmentVariableName;
                AddJobOrStepEnvVar(jobOrStep, envVarName, $"${{{{ secrets.{secret.Secret.SecretVariableName} }}}}");
            }
        }
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
