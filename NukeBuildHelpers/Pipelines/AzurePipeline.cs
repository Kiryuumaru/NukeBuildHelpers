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
using NukeBuildHelpers.Attributes;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Interfaces;
using NukeBuildHelpers.Models;
using Octokit;
using Semver;
using Serilog;
using Serilog.Events;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using YamlDotNet.Core.Tokens;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

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
                triggerType = TriggerType.PullRequest;
                branch = Environment.GetEnvironmentVariable("SYSTEM_PULLREQUEST_TARGETBRANCH")!;
                prNumber = long.Parse(branch.Split('/')[2]);
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
            PrNumber = prNumber
        };
    }

    public void Prepare(PreSetupOutput preSetupOutput, AppConfig appConfig, Dictionary<string, AppRunEntry> toEntry)
    {
        var outputTestMatrix = new Dictionary<string, PreSetupOutputAppTestEntryMatrix>();
        var outputBuildMatrix = new Dictionary<string, PreSetupOutputAppEntryMatrix>();
        var outputPublishMatrix = new Dictionary<string, PreSetupOutputAppEntryMatrix>();

        foreach (var appEntryConfig in appConfig.AppEntryConfigs.Values)
        {
            if (!toEntry.TryGetValue(appEntryConfig.Entry.Id, out var entry))
            {
                continue;
            }
            if ((preSetupOutput.TriggerType == TriggerType.PullRequest && appEntryConfig.Entry.RunBuildOn.HasFlag(RunType.PullRequest)) ||
                (preSetupOutput.TriggerType == TriggerType.Commit && appEntryConfig.Entry.RunBuildOn.HasFlag(RunType.Commit)) ||
                (preSetupOutput.TriggerType == TriggerType.Tag && appEntryConfig.Entry.RunBuildOn.HasFlag(RunType.Bump)))
            {
                outputBuildMatrix.Add(appEntryConfig.Entry.Id, new()
                {
                    Id = appEntryConfig.Entry.Id,
                    Name = appEntryConfig.Entry.Name,
                    Environment = preSetupOutput.Environment,
                    RunsOn = GetRunsOn(appEntryConfig.Entry.BuildRunsOn),
                    BuildScript = GetBuildScript(appEntryConfig.Entry.BuildRunsOn),
                    IdsToRun = appEntryConfig.Entry.Id,
                    Version = entry.Version.ToString()
                });
            }
            if ((preSetupOutput.TriggerType == TriggerType.PullRequest && appEntryConfig.Entry.RunPublishOn.HasFlag(RunType.PullRequest)) ||
                (preSetupOutput.TriggerType == TriggerType.Commit && appEntryConfig.Entry.RunPublishOn.HasFlag(RunType.Commit)) ||
                (preSetupOutput.TriggerType == TriggerType.Tag && appEntryConfig.Entry.RunPublishOn.HasFlag(RunType.Bump)))
            {
                outputPublishMatrix.Add(appEntryConfig.Entry.Id, new()
                {
                    Id = appEntryConfig.Entry.Id,
                    Name = appEntryConfig.Entry.Name,
                    Environment = preSetupOutput.Environment,
                    RunsOn = GetRunsOn(appEntryConfig.Entry.PublishRunsOn),
                    BuildScript = GetBuildScript(appEntryConfig.Entry.PublishRunsOn),
                    IdsToRun = appEntryConfig.Entry.Id,
                    Version = entry.Version.ToString()
                });
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
                ((outputBuildMatrix.ContainsKey(appEntry.Id) || outputPublishMatrix.ContainsKey(appEntry.Id)) && appTestEntry.RunTestOn.HasFlag(RunTestType.Target)))
            {
                PreSetupOutputAppTestEntryMatrix preSetupOutputMatrix = new()
                {
                    Id = appTestEntry.Id,
                    Name = appTestEntry.Name,
                    Environment = preSetupOutput.Environment,
                    RunsOn = GetRunsOn(appTestEntry.RunsOn),
                    BuildScript = GetBuildScript(appTestEntry.RunsOn),
                    IdsToRun = $"{appEntry.Id};{appTestEntry.Id}"
                };
                outputTestMatrix.Add(appTestEntry.Id, preSetupOutputMatrix);
            }
        }
        if (outputTestMatrix.Count == 0 && !appConfig.AppTestEntries.Any(i => i.Value.Enable))
        {
            PreSetupOutputAppTestEntryMatrix preSetupOutputMatrix = new()
            {
                Id = "skip",
                Name = "Skip",
                Environment = preSetupOutput.Environment,
                RunsOn = GetRunsOn(RunsOnType.Ubuntu2204),
                BuildScript = "",
                IdsToRun = ""
            };
            outputTestMatrix.Add("skip", preSetupOutputMatrix);
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
        BaseNukeBuildHelpers.GetOrFail(BaseNukeBuildHelpers.GetAppConfig, out var appConfig);

        List<WorkflowBuilder> workflowBuilders = [.. BaseNukeBuildHelpers.GetInstances<WorkflowBuilder>().OrderByDescending(i => i.Priority)];

        NukeBuild.SetupWorkflowBuilder(workflowBuilders, PipelineType.Azure);

        var appEntrySecretMap = BaseNukeBuildHelpers.GetEntrySecretMap<AppEntry>();
        var appTestEntrySecretMap = BaseNukeBuildHelpers.GetEntrySecretMap<AppTestEntry>();

        Dictionary<string, object> workflow = new()
        {
            ["name"] = "Nuke CICD Pipeline",
            ["trigger"] = new Dictionary<string, object>()
                {
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
        AddJobOutputFromFile(preSetupJob, "NUKE_PRE_SETUP_OUTPUT", "./.nuke/temp/pre_setup_output.json");
        AddJobOutputFromFile(preSetupJob, "NUKE_PRE_SETUP_OUTPUT_TEST_MATRIX", "./.nuke/temp/pre_setup_output_test_matrix.json");
        AddJobOutputFromFile(preSetupJob, "NUKE_PRE_SETUP_OUTPUT_BUILD_MATRIX", "./.nuke/temp/pre_setup_output_build_matrix.json");
        AddJobOutputFromFile(preSetupJob, "NUKE_PRE_SETUP_OUTPUT_PUBLISH_MATRIX", "./.nuke/temp/pre_setup_output_publish_matrix.json");

        needs.Add("pre_setup");

        // ██████████████████████████████████████
        // ████████████████ Test ████████████████
        // ██████████████████████████████████████
        if (appConfig.AppTestEntries.Count > 0)
        {
            var testJob = AddJob(workflow, "test", "Test", "$(runs_on)", needs: [.. needs], condition: "succeeded()");
            AddJobEnvVarFromNeeds(testJob, "NUKE_PRE_SETUP_OUTPUT", "pre_setup");
            AddJobMatrixIncludeFromPreSetup(testJob, "NUKE_PRE_SETUP_OUTPUT_TEST_MATRIX");
            AddJobStepCheckout(testJob, condition: "ne(variables['id'], 'skip')");
            AddJobStepsFromBuilder(testJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPreTestRun(step));
            var nukeTestStep = AddJobStepNukeRun(testJob, "$(build_script)", "PipelineTest", "$(ids_to_run)", condition: "ne(variables['id'], 'skip')");
            AddJobStepsFromBuilder(testJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPostTestRun(step));
            AddStepEnvVarFromSecretMap(nukeTestStep, appTestEntrySecretMap);

            needs.Add("test");
        }

        // ██████████████████████████████████████
        // ███████████████ Build ████████████████
        // ██████████████████████████████████████
        var buildJob = AddJob(workflow, "build", "Build", "$(runs_on)", needs: [.. needs], condition: "and(succeeded(), eq(dependencies.pre_setup.outputs['NUKE_PRE_SETUP_HAS_ENTRIES.NUKE_PRE_SETUP_HAS_ENTRIES'], 'true'))");
        AddJobMatrixIncludeFromPreSetup(buildJob, "NUKE_PRE_SETUP_OUTPUT_BUILD_MATRIX");
        AddJobEnvVarFromNeeds(buildJob, "NUKE_PRE_SETUP_OUTPUT", "pre_setup");
        AddJobStepCheckout(buildJob);
        AddJobStepsFromBuilder(buildJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPreBuildRun(step));
        var nukeBuildStep = AddJobStepNukeRun(buildJob, "$(build_script)", "PipelineBuild", "$(ids_to_run)");
        AddStepEnvVarFromSecretMap(nukeBuildStep, appEntrySecretMap);
        AddJobStepsFromBuilder(buildJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPostBuildRun(step));
        var uploadBuildStep = AddJobStep(buildJob, displayName: "Upload artifacts", task: "PublishPipelineArtifact@1");
        AddJobStepInputs(uploadBuildStep, "artifact", "$(id)");
        AddJobStepInputs(uploadBuildStep, "targetPath", "./.nuke/output");
        AddJobStepInputs(uploadBuildStep, "continueOnError", "true");

        needs.Add("build");

        // ██████████████████████████████████████
        // ██████████████ Publish ███████████████
        // ██████████████████████████████████████
        var publishJob = AddJob(workflow, "publish", "Publish", "$(runs_on)", needs: [.. needs], condition: "and(succeeded(), eq(dependencies.pre_setup.outputs['NUKE_PRE_SETUP_HAS_ENTRIES.NUKE_PRE_SETUP_HAS_ENTRIES'], 'true'))");
        AddJobEnvVarFromNeeds(publishJob, "NUKE_PRE_SETUP_OUTPUT", "pre_setup");
        AddJobMatrixIncludeFromPreSetup(publishJob, "NUKE_PRE_SETUP_OUTPUT_PUBLISH_MATRIX");
        AddJobStepCheckout(publishJob);
        var downloadPublishStep = AddJobStep(publishJob, displayName: "Download artifacts", task: "DownloadPipelineArtifact@2");
        AddJobStepInputs(downloadPublishStep, "artifact", "$(id)");
        AddJobStepInputs(downloadPublishStep, "path", "./.nuke/output");
        AddJobStepInputs(downloadPublishStep, "continueOnError", "true");
        AddJobStepsFromBuilder(publishJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPrePublishRun(step));
        var nukePublishStep = AddJobStepNukeRun(publishJob, "$(build_script)", "PipelinePublish", "$(ids_to_run)");
        AddStepEnvVarFromSecretMap(nukePublishStep, appEntrySecretMap);
        AddJobStepsFromBuilder(publishJob, workflowBuilders, (wb, step) => wb.WorkflowBuilderPostPublishRun(step));

        needs.Add("publish");

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

    private static void AddStepEnvVarFromSecretMap(Dictionary<string, object> step, Dictionary<string, (Type EntryType, List<(MemberInfo MemberInfo, SecretHelperAttribute SecretHelper)> SecretHelpers)> secretMap)
    {
        foreach (var map in secretMap)
        {
            foreach (var secret in map.Value.SecretHelpers)
            {
                var envVarName = string.IsNullOrEmpty(secret.SecretHelper.EnvironmentVariableName) ? $"NUKE_{secret.SecretHelper.SecretVariableName}" : secret.SecretHelper.EnvironmentVariableName;
                AddStepEnvVar(step, envVarName, $"$({secret.SecretHelper.SecretVariableName})");
            }
        }
    }

    private static void AddJobOutputFromFile(Dictionary<string, object> job, string envVarName, string filename)
    {
        AddJobStep(job, name: envVarName, displayName: $"Output {envVarName}",
            script: $"echo \"##vso[task.setvariable variable={envVarName}]$(cat {filename})\" && echo \"##vso[task.setvariable variable={envVarName};isOutput=true]$(cat {filename})\"");
    }
}
