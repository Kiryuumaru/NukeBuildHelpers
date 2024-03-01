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
    private readonly Deserializer _yamlDeserializer = new();
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

    private static Dictionary<string, object> GenerateGithubWorkflowJob(Dictionary<string, object> workflow, string id, string name, string runsOn)
    {
        Dictionary<string, object> job = new()
        {
            ["name"] = name,
            ["runs-on"] = runsOn,
            ["steps"] = new List<object>()
        };
        ((Dictionary<string, object>)workflow["jobs"])[id] = job;
        return job;
    }

    private static Dictionary<string, object> GenerateGithubWorkflowJob(Dictionary<string, object> workflow, string id, string name, RunsOnType buildsOnType)
    {
        return GenerateGithubWorkflowJob(workflow, id, name, GetRunsOnGithub(buildsOnType));
    }

    private static Dictionary<string, object> GenerateGithubWorkflowJobStep(Dictionary<string, object> job, string id = "", string name = "", string uses = "")
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
        return step;
    }

    private static Dictionary<string, object> GenerateGithubWorkflowJobMatrixInclude(Dictionary<string, object> job)
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
                            { "branches", new string[] { "master", "alpha" } }
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
            var preSetup = GenerateGithubWorkflowJob(workflow, "pre_setup", "Pre Setup", RunsOnType.Ubuntu2204);
            GenerateGithubWorkflowJobStep(preSetup, uses: "actions/checkout@v4");

            // ██████████████████████████████████████
            // ████████████████ Test ████████████████
            // ██████████████████████████████████████
            if (appTestEntries.Count > 0)
            {
                var test = GenerateGithubWorkflowJob(workflow, "test", "Test - ${{ matrix.name }}", "${{ matrix.runs_on }}");
                test["needs"] = needs.ToArray();
                needs.Add("test");
                foreach (var appTestEntry in appTestEntries)
                {
                    var include = GenerateGithubWorkflowJobMatrixInclude(test);
                    include["id"] = appTestEntry.Id;
                    include["name"] = appTestEntry.Name;
                    include["runs_on"] = GetRunsOnGithub(appTestEntry.RunsOn);
                    include["build_script"] = GetBuildScriptGithub(appTestEntry.RunsOn);
                }
                GenerateGithubWorkflowJobStep(test, uses: "actions/checkout@v4");
                var nukeTest = GenerateGithubWorkflowJobStep(test, name: "Run Nuke");
                nukeTest.Add("run", "${{ matrix.build_script }} test ${{ matrix.id }}");
            }

            // ██████████████████████████████████████
            // ███████████████ Build ████████████████
            // ██████████████████████████████████████
            var build = GenerateGithubWorkflowJob(workflow, "build", "Build - ${{ matrix.name }}", "${{ matrix.runs_on }}");
            build["needs"] = needs.ToArray();
            needs.Add("build");
            foreach (var appEntry in appEntries)
            {
                var include = GenerateGithubWorkflowJobMatrixInclude(build);
                include["id"] = appEntry.Id;
                include["name"] = appEntry.Name;
                include["runs_on"] = GetRunsOnGithub(appEntry.RunsOn);
            }
            GenerateGithubWorkflowJobStep(build, uses: "actions/checkout@v4");
            var nukeBuild = GenerateGithubWorkflowJobStep(build, name: "Run Nuke");
            nukeBuild.Add("run", "${{ matrix.build_script }} pack ${{ matrix.id }}");

            // ██████████████████████████████████████
            // ██████████████ Publish ███████████████
            // ██████████████████████████████████████
            var publish = GenerateGithubWorkflowJob(workflow, "publish", "Publish - ${{ matrix.name }}", "${{ matrix.runs_on }}");
            publish["needs"] = needs.ToArray();
            foreach (var appEntry in appEntries)
            {
                var include = GenerateGithubWorkflowJobMatrixInclude(publish);
                include["id"] = appEntry.Id;
                include["name"] = appEntry.Name;
                include["runs_on"] = GetRunsOnGithub(appEntry.RunsOn);
            }
            GenerateGithubWorkflowJobStep(publish, uses: "actions/checkout@v4");

            // ██████████████████████████████████████
            // ██████████████ Release ███████████████
            // ██████████████████████████████████████
            var release = GenerateGithubWorkflowJob(workflow, "release", "Release", RunsOnType.Ubuntu2204);
            release["needs"] = needs.ToArray();
            GenerateGithubWorkflowJobStep(release, uses: "actions/checkout@v4");

            needs.Add("publish");
            needs.Add("release");

            // ██████████████████████████████████████
            // █████████████ Post Setup █████████████
            // ██████████████████████████████████████
            var postSetup = GenerateGithubWorkflowJob(workflow, "post_setup", $"Post Setup", RunsOnType.Ubuntu2204);
            postSetup["needs"] = needs.ToArray();
            GenerateGithubWorkflowJobStep(postSetup, uses: "actions/checkout@v4");

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
