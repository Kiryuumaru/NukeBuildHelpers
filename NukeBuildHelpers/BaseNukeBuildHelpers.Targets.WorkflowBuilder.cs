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

    private static Dictionary<string, object> AddGithubWorkflowJob(Dictionary<string, object> workflow, string id, string name, string runsOn, IEnumerable<string> needs = null)
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

    private static Dictionary<string, object> AddGithubWorkflowJob(Dictionary<string, object> workflow, string id, string name, RunsOnType buildsOnType, IEnumerable<string> needs = null)
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

    private static Dictionary<string, object> AddGithubWorkflowJobMatrixInclude(Dictionary<string, object> job)
    {
        Dictionary<string, object> include = new();
        if (!job.ContainsKey("strategy"))
        {
            job["strategy"] = new Dictionary<string, object>();
        }
        if (!((Dictionary<string, object>)job["strategy"]).ContainsKey("matrix"))
        {
            ((Dictionary<string, object>)job["strategy"])["matrix"] = new Dictionary<string, object>();
        }
        if (!((Dictionary<string, object>)((Dictionary<string, object>)job["strategy"])["matrix"]).ContainsKey("include"))
        {
            ((Dictionary<string, object>)((Dictionary<string, object>)job["strategy"])["matrix"])["include"] = new List<object>();
        }
        ((List<object>)((Dictionary<string, object>)((Dictionary<string, object>)job["strategy"])["matrix"])["include"]).Add(include);
        return include;
    }

    private static void AddGithubWorkflowJobOutput(Dictionary<string, object> job, string outputName, string fromStepId, string fromStepVariable)
    {
        if (!job.ContainsKey("outputs"))
        {
            job["outputs"] = new Dictionary<string, object>();
        }
        ((Dictionary<string, object>)job["outputs"]).Add(outputName, $"${{{{ steps.{fromStepId}.outputs.{fromStepVariable} }}}}");
    }

    private static void AddGithubWorkflowJobEnvVar(Dictionary<string, object> job, string envVarName, string envVarValue)
    {
        if (!job.ContainsKey("env"))
        {
            job["env"] = new Dictionary<string, object>();
        }
        ((Dictionary<string, object>)job["env"]).Add(envVarName, envVarValue);
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
                            { "branches", new string[] { "*" } },
                            { "tags", new string[] { "**" } }
                        }
                    },
                    { "pull_request", new Dictionary<string, object>()
                        {
                            { "branches", new string[] { "fix/**", "feat/**", "hotfix/**" } }
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
            var preSetup = AddGithubWorkflowJob(workflow, "pre_setup", "Pre Setup", RunsOnType.Ubuntu2204);
            AddGithubWorkflowJobStep(preSetup, uses: "actions/checkout@v4");
            AddGithubWorkflowJobStep(preSetup, id: "setup", name: "Run Nuke",
                run: $"{GetBuildScriptGithub(RunsOnType.Ubuntu2204)} PipelinePreSetup && echo \"PRE_SETUP_OUTPUT=$(cat ./.nuke/temp/pre_setup_output.json)\" >> $GITHUB_OUTPUT");
            AddGithubWorkflowJobOutput(preSetup, "PRE_SETUP_OUTPUT", "setup", "PRE_SETUP_OUTPUT");

            // ██████████████████████████████████████
            // ████████████████ Test ████████████████
            // ██████████████████████████████████████
            if (appTestEntries.Count > 0)
            {
                var test = AddGithubWorkflowJob(workflow, "test", "Test - ${{ matrix.name }}", "${{ matrix.runs_on }}");
                test["needs"] = needs.ToArray();
                needs.Add("test");
                AddGithubWorkflowJobEnvVarFromNeeds(test, "PRE_SETUP_OUTPUT", "pre_setup", "PRE_SETUP_OUTPUT");
                foreach (var appTestEntry in appTestEntries)
                {
                    var appEntry = appEntryConfigs.First(i => i.Value.Tests.Any(j => j.Id == appTestEntry.Id)).Value.Entry;
                    var include = AddGithubWorkflowJobMatrixInclude(test);
                    include["id"] = appTestEntry.Id;
                    include["name"] = appTestEntry.Name;
                    include["runs_on"] = GetRunsOnGithub(appTestEntry.RunsOn);
                    include["build_script"] = GetBuildScriptGithub(appTestEntry.RunsOn);
                    include["ids_to_run"] = $"{appEntry.Id};{appTestEntry.Id}";
                }
                AddGithubWorkflowJobStep(test, uses: "actions/checkout@v4");
                AddGithubWorkflowJobStep(test, name: "Run Nuke Prepare", run: "${{ matrix.build_script }} PipelinePrepare --args \"${{ matrix.ids_to_run }}\"");
                AddGithubWorkflowJobStep(test, name: "Run Nuke Test", run: "${{ matrix.build_script }} PipelineTest --args \"${{ matrix.ids_to_run }}\"");
            }

            // ██████████████████████████████████████
            // ███████████████ Build ████████████████
            // ██████████████████████████████████████
            var build = AddGithubWorkflowJob(workflow, "build", "Build - ${{ matrix.name }}", "${{ matrix.runs_on }}", needs.ToArray());
            AddGithubWorkflowJobEnvVarFromNeeds(build, "PRE_SETUP_OUTPUT", "pre_setup", "PRE_SETUP_OUTPUT");
            foreach (var appEntry in appEntries)
            {
                var include = AddGithubWorkflowJobMatrixInclude(build);
                include["id"] = appEntry.Id;
                include["name"] = appEntry.Name;
                include["runs_on"] = GetRunsOnGithub(appEntry.RunsOn);
                include["build_script"] = GetBuildScriptGithub(appEntry.RunsOn);
                include["ids_to_run"] = appEntry.Id;
            }
            AddGithubWorkflowJobStep(build, uses: "actions/checkout@v4");
            AddGithubWorkflowJobStep(build, name: "Run Nuke Prepare", run: "${{ matrix.build_script }} PipelinePrepare --args \"${{ matrix.ids_to_run }}\"");
            AddGithubWorkflowJobStep(build, name: "Run Nuke Build", run: "${{ matrix.build_script }} PipelineBuild --args \"${{ matrix.ids_to_run }}\"");
            AddGithubWorkflowJobStep(build, name: "Run Nuke Pack", run: "${{ matrix.build_script }} PipelinePack --args \"${{ matrix.ids_to_run }}\"");

            needs.Add("build");

            // ██████████████████████████████████████
            // ██████████████ Publish ███████████████
            // ██████████████████████████████████████
            var publish = AddGithubWorkflowJob(workflow, "publish", "Publish - ${{ matrix.name }}", "${{ matrix.runs_on }}", needs.ToArray());
            AddGithubWorkflowJobEnvVarFromNeeds(publish, "PRE_SETUP_OUTPUT", "pre_setup", "PRE_SETUP_OUTPUT");
            foreach (var appEntry in appEntries)
            {
                var include = AddGithubWorkflowJobMatrixInclude(publish);
                include["id"] = appEntry.Id;
                include["name"] = appEntry.Name;
                include["runs_on"] = GetRunsOnGithub(appEntry.RunsOn);
                include["build_script"] = GetBuildScriptGithub(appEntry.RunsOn);
                include["ids_to_run"] = appEntry.Id;
            }
            AddGithubWorkflowJobStep(publish, uses: "actions/checkout@v4");
            AddGithubWorkflowJobStep(publish, name: "Run Nuke Publish", run: "${{ matrix.build_script }} PipelinePublish --args \"${{ matrix.ids_to_run }}\"");

            // ██████████████████████████████████████
            // ██████████████ Release ███████████████
            // ██████████████████████████████████████
            var release = AddGithubWorkflowJob(workflow, "release", "Release", RunsOnType.Ubuntu2204, needs.ToArray());
            AddGithubWorkflowJobEnvVarFromNeeds(release, "PRE_SETUP_OUTPUT", "pre_setup", "PRE_SETUP_OUTPUT");
            AddGithubWorkflowJobStep(release, uses: "actions/checkout@v4");

            needs.Add("publish");
            needs.Add("release");

            // ██████████████████████████████████████
            // █████████████ Post Setup █████████████
            // ██████████████████████████████████████
            var postSetup = AddGithubWorkflowJob(workflow, "post_setup", $"Post Setup", RunsOnType.Ubuntu2204, needs.ToArray());
            AddGithubWorkflowJobEnvVarFromNeeds(postSetup, "PRE_SETUP_OUTPUT", "pre_setup", "PRE_SETUP_OUTPUT");
            AddGithubWorkflowJobStep(postSetup, uses: "actions/checkout@v4");

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
