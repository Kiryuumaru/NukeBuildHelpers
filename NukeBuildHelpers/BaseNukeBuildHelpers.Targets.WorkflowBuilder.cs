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

    private static Dictionary<string, object> GenerateGithubWorkflowJobStep(Dictionary<string, object> job, string uses)
    {
        Dictionary<string, object> step = new()
        {
            ["uses"] = uses,
        };
        ((List<object>)job["steps"]).Add(step);
        return job;
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

            var preSetup = GenerateGithubWorkflowJob(workflow, "pre_setup", "Pre Setup", RunsOnType.Ubuntu2204);
            GenerateGithubWorkflowJobStep(preSetup, "actions/checkout@v3");

            if (appTestEntries.Count > 0)
            {
                var test = GenerateGithubWorkflowJob(workflow, $"test", "Test - ${{ matrix.name }}", "${{ matrix.runs_on }}");
                test["needs"] = new string[] { "pre_setup" };
                foreach (var appTestEntry in appTestEntries)
                {
                    var include = GenerateGithubWorkflowJobMatrixInclude(test);
                    include["id"] = appTestEntry.Id;
                    include["name"] = appTestEntry.Name;
                    include["runs_on"] = GetRunsOnGithub(appTestEntry.RunsOn);
                }
                GenerateGithubWorkflowJobStep(test, "actions/checkout@v3");
            }

            //foreach (var appTestEntry in appTestEntries)
            //{
            //    var test = GenerateGithubWorkflowJob(workflow, $"test_{appTestEntry.Id}", $"Test - {appTestEntry.Name}", appTestEntry.RunsOn);
            //    test["needs"] = new string[] { "setup" };
            //    var checkout = GenerateGithubWorkflowJobStep(test, "actions/checkout@v3");
            //}

            //foreach (var appEntryConfig in appEntryConfigs)
            //{
            //    var build = GenerateGithubWorkflowJob(workflow, $"build_{appEntryConfig.Value.Entry.Id}", $"Build - {appEntryConfig.Value.Entry.Name}", appEntryConfig.Value.Entry.RunsOn);
            //    build["needs"] = new string[] { "setup" }
            //        .Combine(appEntryConfig.Value.Tests.Select(i => $"test_{i.Id}"));
            //    var checkout = GenerateGithubWorkflowJobStep(build, "actions/checkout@v3");
            //}

            //var release = GenerateGithubWorkflowJob(workflow, "release", $"Release", RunsOnType.Ubuntu2204);
            //release["needs"] = new string[] { "setup" };
            //GenerateGithubWorkflowJobStep(release, "actions/checkout@v3");

            //foreach (var appEntryConfig in appEntryConfigs)
            //{
            //    var publish = GenerateGithubWorkflowJob(workflow, $"publish_{appEntryConfig.Value.Entry.Id}", $"Publish - {appEntryConfig.Value.Entry.Name}", appEntryConfig.Value.Entry.RunsOn);
            //    publish["needs"] = new string[] { "setup" }
            //        .Combine(new string[] { $"build_{appEntryConfig.Value.Entry.Id}" })
            //        .Combine(appEntryConfig.Value.Tests.Select(i => $"test_{i.Id}"));
            //    var checkout = GenerateGithubWorkflowJobStep(publish, "actions/checkout@v3");
            //}

            var postSetup = GenerateGithubWorkflowJob(workflow, "post_setup", $"Post Setup", RunsOnType.Ubuntu2204);
            postSetup["needs"] = new string[] { "pre_setup" };
            GenerateGithubWorkflowJobStep(postSetup, "actions/checkout@v3");

            var workflowDirPath = RootDirectory / ".github" / "workflows";
            var workflowPath = workflowDirPath / "nuke-cicd.yml";

            Directory.CreateDirectory(workflowDirPath);
            File.WriteAllText(workflowPath, _yamlSerializer.Serialize(workflow));

            Log.Information("Workflow built at " + workflowPath.ToString());
        });
}
