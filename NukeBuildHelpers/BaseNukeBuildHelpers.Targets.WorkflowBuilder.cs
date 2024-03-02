using ICSharpCode.SharpZipLib.Zip;
using Nuke.Common;
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
using static System.Net.Mime.MediaTypeNames;

namespace NukeBuildHelpers;

partial class BaseNukeBuildHelpers
{
    private readonly Serializer _yamlSerializer = new();

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

    private static Dictionary<string, object> AddGithubWorkflowJobStep(Dictionary<string, object> job, string id = "", string name = "", string uses = "", string run = "")
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
        return step;
    }

    private static readonly string[] valueArray0 = new string[] { "**" };
    private static readonly string[] valueArray = new string[] { "*" };
    private static readonly string[] value = new string[] { "fix/**", "feat/**", "hotfix/**" };

    private static void AddGithubWorkflowJobStepWith(Dictionary<string, object> step, string name, string value)
    {
        if (!step.TryGetValue("with", out object? withValue))
        {
            withValue = new Dictionary<string, object>();
            step["with"] = withValue;
        }
        ((Dictionary<string, object>)withValue).Add(name, value);
    }

    private static Dictionary<string, object> AddGithubWorkflowJobMatrixInclude(Dictionary<string, object> job)
    {
        Dictionary<string, object> include = new();
        if (!job.TryGetValue("strategy", out object? value))
        {
            value = new Dictionary<string, object>();
            job["strategy"] = value;
        }
        if (!((Dictionary<string, object>)value).ContainsKey("matrix"))
        {
            ((Dictionary<string, object>)value)["matrix"] = new Dictionary<string, object>();
        }
        if (!((Dictionary<string, object>)((Dictionary<string, object>)value)["matrix"]).ContainsKey("include"))
        {
            ((Dictionary<string, object>)((Dictionary<string, object>)value)["matrix"])["include"] = new List<object>();
        }
        ((List<object>)((Dictionary<string, object>)((Dictionary<string, object>)value)["matrix"])["include"]).Add(include);
        return include;
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
                            { "branches", valueArray },
                            { "tags", valueArray0 }
                        }
                    },
                    { "pull_request", new Dictionary<string, object>()
                        {
                            { "branches", value }
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
            AddGithubWorkflowJobStepWith(cachePreSetupStep, "key", "${{ runner.os }}-nuget-pre_setup-${{ hashFiles('**/*.csproj') }}");
            AddGithubWorkflowJobStepWith(cachePreSetupStep, "restore-keys", "${{ runner.os }}-nuget-pre_setup-");
            AddGithubWorkflowJobStep(preSetupJob, id: "setup", name: "Run Nuke",
                run: $"{GetBuildScriptGithub(RunsOnType.Ubuntu2204)} PipelinePreSetup && echo \"PRE_SETUP_OUTPUT=$(cat ./.nuke/temp/pre_setup_output.json)\" >> $GITHUB_OUTPUT");
            AddGithubWorkflowJobOutput(preSetupJob, "PRE_SETUP_OUTPUT", "setup", "PRE_SETUP_OUTPUT");

            // ██████████████████████████████████████
            // ████████████████ Test ████████████████
            // ██████████████████████████████████████
            if (appTestEntries.Count > 0)
            {
                var testJob = AddGithubWorkflowJob(workflow, "test", "Test - ${{ matrix.name }}", "${{ matrix.runs_on }}");
                testJob["needs"] = needs.ToArray();
                needs.Add("test");
                AddGithubWorkflowJobEnvVarFromNeeds(testJob, "PRE_SETUP_OUTPUT", "pre_setup", "PRE_SETUP_OUTPUT");
                foreach (var appTestEntry in appTestEntries)
                {
                    var appEntry = appEntryConfigs.First(i => i.Value.Tests.Any(j => j.Id == appTestEntry.Id)).Value.Entry;
                    var matrixInclude = AddGithubWorkflowJobMatrixInclude(testJob);
                    matrixInclude["id"] = appTestEntry.Id;
                    matrixInclude["name"] = appTestEntry.Name;
                    matrixInclude["runs_on"] = GetRunsOnGithub(appTestEntry.RunsOn);
                    matrixInclude["build_script"] = GetBuildScriptGithub(appTestEntry.RunsOn);
                    matrixInclude["ids_to_run"] = $"{appEntry.Id};{appTestEntry.Id}";
                }
                AddGithubWorkflowJobStep(testJob, uses: "actions/checkout@v4");
                var cacheTestStep = AddGithubWorkflowJobStep(testJob, uses: "actions/cache@v4");
                AddGithubWorkflowJobStepWith(cacheTestStep, "path", "~/.nuget/packages");
                AddGithubWorkflowJobStepWith(cacheTestStep, "key", "${{ runner.os }}-nuget-test-${{ hashFiles('**/*.csproj') }}");
                AddGithubWorkflowJobStepWith(cacheTestStep, "restore-keys", "${{ runner.os }}-nuget-test-");
                AddGithubWorkflowJobStep(testJob, name: "Run Nuke Test", run: "${{ matrix.build_script }} PipelineTest --args \"${{ matrix.ids_to_run }}\"");
            }

            // ██████████████████████████████████████
            // ███████████████ Build ████████████████
            // ██████████████████████████████████████
            var buildJob = AddGithubWorkflowJob(workflow, "build", "Build - ${{ matrix.name }}", "${{ matrix.runs_on }}", needs.ToArray());
            AddGithubWorkflowJobEnvVarFromNeeds(buildJob, "PRE_SETUP_OUTPUT", "pre_setup", "PRE_SETUP_OUTPUT");
            foreach (var appEntry in appEntries)
            {
                var matrixInclude = AddGithubWorkflowJobMatrixInclude(buildJob);
                matrixInclude["id"] = appEntry.Id;
                matrixInclude["name"] = appEntry.Name;
                matrixInclude["runs_on"] = GetRunsOnGithub(appEntry.BuildRunsOn);
                matrixInclude["build_script"] = GetBuildScriptGithub(appEntry.BuildRunsOn);
                matrixInclude["ids_to_run"] = appEntry.Id;
            }
            AddGithubWorkflowJobStep(buildJob, uses: "actions/checkout@v4");
            var cacheBuildStep = AddGithubWorkflowJobStep(buildJob, uses: "actions/cache@v4");
            AddGithubWorkflowJobStepWith(cacheBuildStep, "path", "~/.nuget/packages");
            AddGithubWorkflowJobStepWith(cacheBuildStep, "key", "${{ runner.os }}-nuget-build-${{ hashFiles('**/*.csproj') }}");
            AddGithubWorkflowJobStepWith(cacheBuildStep, "restore-keys", "${{ runner.os }}-nuget-build-");
            AddGithubWorkflowJobStep(buildJob, name: "Run Nuke Build", run: "${{ matrix.build_script }} PipelineBuild --args \"${{ matrix.ids_to_run }}\"");
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
            foreach (var appEntry in appEntries)
            {
                var include = AddGithubWorkflowJobMatrixInclude(publishJob);
                include["id"] = appEntry.Id;
                include["name"] = appEntry.Name;
                include["runs_on"] = GetRunsOnGithub(appEntry.PublishRunsOn);
                include["build_script"] = GetBuildScriptGithub(appEntry.PublishRunsOn);
                include["ids_to_run"] = appEntry.Id;
            }
            AddGithubWorkflowJobStep(publishJob, uses: "actions/checkout@v4");
            var cachePublishStep = AddGithubWorkflowJobStep(publishJob, uses: "actions/cache@v4");
            AddGithubWorkflowJobStepWith(cachePublishStep, "path", "~/.nuget/packages");
            AddGithubWorkflowJobStepWith(cachePublishStep, "key", "${{ runner.os }}-nuget-publish-${{ hashFiles('**/*.csproj') }}");
            AddGithubWorkflowJobStepWith(cachePublishStep, "restore-keys", "${{ runner.os }}-nuget-publish-");
            var downloadBuildStep = AddGithubWorkflowJobStep(publishJob, name: "Download artifacts", uses: "actions/download-artifact@v4");
            AddGithubWorkflowJobStepWith(downloadBuildStep, "path", "./.nuke/temp/output");
            AddGithubWorkflowJobStepWith(downloadBuildStep, "pattern", "${{ matrix.id }}");
            AddGithubWorkflowJobStepWith(downloadBuildStep, "merge-multiple", "true");
            AddGithubWorkflowJobStep(publishJob, name: "Run Nuke Publish", run: "${{ matrix.build_script }} PipelinePublish --args \"${{ matrix.ids_to_run }}\"");

            // ██████████████████████████████████████
            // ██████████████ Release ███████████████
            // ██████████████████████████████████████
            var releaseJob = AddGithubWorkflowJob(workflow, "release", "Release", RunsOnType.Ubuntu2204, needs.ToArray());
            AddGithubWorkflowJobEnvVarFromNeeds(releaseJob, "PRE_SETUP_OUTPUT", "pre_setup", "PRE_SETUP_OUTPUT");
            AddGithubWorkflowJobStep(releaseJob, uses: "actions/checkout@v4");

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
