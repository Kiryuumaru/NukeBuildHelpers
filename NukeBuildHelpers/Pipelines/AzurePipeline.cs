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
        if (string.IsNullOrEmpty(branch))
        {
            branch = NukeBuild.Repository.Branch;
        }
        else
        {
            if (branch.StartsWith("refs/pull", StringComparison.OrdinalIgnoreCase))
            {
                triggerType = TriggerType.PullRequest;
                branch = Environment.GetEnvironmentVariable("SYSTEM_PULLREQUEST_TARGETBRANCH")!;
            }
            else if (branch.StartsWith("refs/tags", StringComparison.OrdinalIgnoreCase))
            {
                triggerType = TriggerType.Tag;
                branch = NukeBuild.Git.Invoke($"branch -r --contains {branch}").FirstOrDefault().Text;
                branch = branch[(branch.IndexOf('/') + 1)..];
            }
            else if (branch.StartsWith("refs/heads", StringComparison.OrdinalIgnoreCase))
            {
                triggerType = TriggerType.Commit;
                branch = branch[11..];
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
        var outputTestMatrix = new Dictionary<string, PreSetupOutputAppTestEntryMatrix>();
        var outputBuildMatrix = new Dictionary<string, PreSetupOutputAppEntryMatrix>();
        var outputPublishMatrix = new Dictionary<string, PreSetupOutputAppEntryMatrix>();
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
                    RunsOn = GetRunsOn(appTestEntry.RunsOn),
                    BuildScript = GetBuildScript(appTestEntry.RunsOn),
                    IdsToRun = $"{appEntry.Id};{appTestEntry.Id}"
                };
                outputTestMatrix.Add(appTestEntry.Id, preSetupOutputMatrix);
            }
        }
        if (outputTestMatrix.Count == 0 && appTestEntries.Count != 0)
        {
            PreSetupOutputAppTestEntryMatrix preSetupOutputMatrix = new()
            {
                Id = "skip",
                Name = "Skip",
                RunsOn = GetRunsOn(RunsOnType.Ubuntu2204),
                BuildScript = "",
                IdsToRun = ""
            };
            outputTestMatrix.Add("skip", preSetupOutputMatrix);
        }
        foreach (var (Entry, Tests) in appEntryConfigs.Values)
        {
            var release = toRelease.FirstOrDefault(i => i.AppEntry.Id == Entry.Id);
            if (release.AppEntry != null)
            {
                outputBuildMatrix.Add(Entry.Id, new()
                {
                    Id = Entry.Id,
                    Name = Entry.Name,
                    RunsOn = GetRunsOn(Entry.BuildRunsOn),
                    BuildScript = GetBuildScript(Entry.BuildRunsOn),
                    IdsToRun = Entry.Id,
                    Version = release.Version.ToString() + "+build." + GitHubActions.Instance.RunId,
                });
                outputPublishMatrix.Add(Entry.Id, new()
                {
                    Id = Entry.Id,
                    Name = Entry.Name,
                    RunsOn = GetRunsOn(Entry.PublishRunsOn),
                    BuildScript = GetBuildScript(Entry.PublishRunsOn),
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
            ["trigger"] = new Dictionary<string, object>()
                {
                    { "branches", new Dictionary<string, object>()
                        {
                            { "include", new List<string> { "*" } },
                        }
                    },
                    { "tags", new Dictionary<string, object>()
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
        AddStepEnvVar(nukePreSetupStep, "GITHUB_TOKEN", "$(GITHUB_PAT)");
        AddJobOutputFromFile(preSetupJob, "PRE_SETUP_HAS_RELEASE", "./.nuke/temp/pre_setup_has_release.txt");
        AddJobOutputFromFile(preSetupJob, "PRE_SETUP_OUTPUT", "./.nuke/temp/pre_setup_output.json");
        AddJobOutputFromFile(preSetupJob, "PRE_SETUP_OUTPUT_TEST_MATRIX", "./.nuke/temp/pre_setup_output_test_matrix.json");
        AddJobOutputFromFile(preSetupJob, "PRE_SETUP_OUTPUT_BUILD_MATRIX", "./.nuke/temp/pre_setup_output_build_matrix.json");
        AddJobOutputFromFile(preSetupJob, "PRE_SETUP_OUTPUT_PUBLISH_MATRIX", "./.nuke/temp/pre_setup_output_publish_matrix.json");

        needs.Add("pre_setup");

        // ██████████████████████████████████████
        // ████████████████ Test ████████████████
        // ██████████████████████████████████████
        if (appTestEntries.Count > 0)
        {
            var testJob = AddJob(workflow, "test", "Test", "$(runs_on)", needs: [.. needs]);
            AddJobEnvVarFromNeeds(testJob, "PRE_SETUP_OUTPUT", "pre_setup");
            AddJobMatrixIncludeFromPreSetup(testJob, "PRE_SETUP_OUTPUT_TEST_MATRIX");
            AddJobStepCheckout(testJob, condition: "ne(variables['id'], 'skip')");
            var nukeTestStep = AddJobStepNukeRun(testJob, "$(build_script)", "PipelineTest", "$(ids_to_run)", condition: "ne(variables['id'], 'skip')");
            AddStepEnvVarFromSecretMap(nukeTestStep, appTestEntrySecretMap);

            needs.Add("test");
        }

        // ██████████████████████████████████████
        // ███████████████ Build ████████████████
        // ██████████████████████████████████████
        var buildJob = AddJob(workflow, "build", "Build", "$(runs_on)", needs: [.. needs], condition: "eq(dependencies.pre_setup.outputs['PRE_SETUP_HAS_RELEASE.PRE_SETUP_HAS_RELEASE'], 'true')");
        AddJobMatrixIncludeFromPreSetup(buildJob, "PRE_SETUP_OUTPUT_BUILD_MATRIX");
        AddJobEnvVarFromNeeds(buildJob, "PRE_SETUP_OUTPUT", "pre_setup");
        AddJobStepCheckout(buildJob);
        var nukeBuildStep = AddJobStepNukeRun(buildJob, "$(build_script)", "PipelineBuild", "$(ids_to_run)");
        AddStepEnvVarFromSecretMap(nukeBuildStep, appEntrySecretMap);
        var uploadBuildStep = AddJobStep(buildJob, displayName: "Upload artifacts", task: "PublishPipelineArtifact@1");
        AddJobStepInputs(uploadBuildStep, "artifact", "$(id)");
        AddJobStepInputs(uploadBuildStep, "targetPath", "./.nuke/temp/output/*");

        needs.Add("build");

        // ██████████████████████████████████████
        // ██████████████ Publish ███████████████
        // ██████████████████████████████████████
        var publishJob = AddJob(workflow, "publish", "Publish", "$(runs_on)", needs: [.. needs], condition: "eq(dependencies.pre_setup.outputs['PRE_SETUP_HAS_RELEASE.PRE_SETUP_HAS_RELEASE'], 'true')");
        AddJobEnvVarFromNeeds(publishJob, "PRE_SETUP_OUTPUT", "pre_setup");
        AddJobMatrixIncludeFromPreSetup(publishJob, "PRE_SETUP_OUTPUT_PUBLISH_MATRIX");
        AddJobStepCheckout(publishJob);
        var downloadBuildStep = AddJobStep(publishJob, displayName: "Download artifacts", task: "DownloadPipelineArtifact@2");
        AddJobStepInputs(downloadBuildStep, "artifact", "$(id)");
        AddJobStepInputs(downloadBuildStep, "path", "./.nuke/temp/output");
        var nukePublishStep = AddJobStepNukeRun(publishJob, "$(build_script)", "PipelinePublish", "$(ids_to_run)");
        AddStepEnvVarFromSecretMap(nukePublishStep, appEntrySecretMap);
        AddJobOutputFromFile(publishJob, "PUBLISH_OUTPUT_SUCCESS", "./.nuke/temp/publish_success.txt");

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

    private static void AddJobEnvVarFromNeeds(Dictionary<string, object> jobOrStep, string envVarName, string needsId)
    {
        AddJobEnvVar(jobOrStep, envVarName, $"$[ dependencies.{needsId}.outputs['{envVarName}.{envVarName}'] ]");
    }

    private static void AddStepEnvVarFromSecretMap(Dictionary<string, object> jobOrStep, Dictionary<string, (Type EntryType, List<(MemberInfo MemberInfo, SecretHelperAttribute SecretHelper)> SecretHelpers)> secretMap)
    {
        foreach (var map in secretMap)
        {
            foreach (var secrets in map.Value.SecretHelpers)
            {
                AddStepEnvVar(jobOrStep, secrets.SecretHelper.Name, $"$({secrets.SecretHelper.Name})");
            }
        }
    }

    private static void AddJobOutputFromFile(Dictionary<string, object> job, string envVarName, string filename)
    {
        AddJobStep(job, name: envVarName, displayName: $"Output {envVarName}",
            script: $"echo \"##vso[task.setvariable variable={envVarName}]$(cat {filename})\" && echo \"##vso[task.setvariable variable={envVarName};isOutput=true]$(cat {filename})\"");
    }
}
