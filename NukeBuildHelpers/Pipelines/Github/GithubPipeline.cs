using Nuke.Common;
using Nuke.Common.IO;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Entry.Helpers;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.Pipelines.Common.Enums;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.Pipelines.Common.Models;
using NukeBuildHelpers.Pipelines.Github.Interfaces;
using NukeBuildHelpers.Pipelines.Github.Models;
using NukeBuildHelpers.Runner.Abstraction;
using Serilog;
using System.Text.Json;
using NukeBuildHelpers.Entry.Enums;
using NukeBuildHelpers.Pipelines.Azure.Interfaces;
using NukeBuildHelpers.Pipelines.Azure.Models;
using NukeBuildHelpers.Entry.Extensions;

namespace NukeBuildHelpers.Pipelines.Github;

internal class GithubPipeline(BaseNukeBuildHelpers nukeBuild) : IPipeline
{
    private readonly string artifactNameSeparator = "___";

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

    public PipelinePreSetup GetPipelinePreSetup()
    {
        string? pipelinePreSetupValue = Environment.GetEnvironmentVariable("NUKE_PRE_SETUP");

        if (string.IsNullOrEmpty(pipelinePreSetupValue))
        {
            throw new Exception("NUKE_PRE_SETUP is empty");
        }

        PipelinePreSetup? pipelinePreSetup = JsonSerializer.Deserialize<PipelinePreSetup>(pipelinePreSetupValue, JsonExtension.SnakeCaseNamingOption);

        return pipelinePreSetup ?? throw new Exception("NUKE_PRE_SETUP is empty");
    }

    public Task PreparePreSetup(AllEntry allEntry)
    {
        return Task.CompletedTask;
    }

    public async Task FinalizePreSetup(AllEntry allEntry, PipelinePreSetup? pipelinePreSetup)
    {
        if (pipelinePreSetup == null)
        {
            return;
        }

        var runClassification = pipelinePreSetup.TriggerType == TriggerType.PullRequest ? "pr." + pipelinePreSetup.PullRequestNumber : "main";
        var runIdentifier = Guid.NewGuid().Encode();

        async Task setupEntryEnv(string entryId, string cacheFamily)
        {
            if (!pipelinePreSetup.EntrySetupMap.TryGetValue(entryId, out var entrySetup))
            {
                return;
            }

            RunnerGithubPipelineOS runnerPipelineOS = JsonSerializer.Deserialize<RunnerGithubPipelineOS>(entrySetup.RunnerOSSetup.RunnerPipelineOS, JsonExtension.SnakeCaseNamingOptionIndented)!;

            string runsOn = ResolveRunsOn(runnerPipelineOS, false).ToString()!;

            var osName = entrySetup.RunnerOSSetup.Name.Replace("-", ".");
            var entryIdNorm = entryId.Replace("-", ".");
            var environmentNorm = pipelinePreSetup.Environment.Replace("-", ".");
            var cacheFamilyNorm = cacheFamily.Replace("-", ".");
            var cacheInvalidatorNorm = entrySetup.CacheInvalidator.Replace("-", ".");
            var runClassificationNorm = runClassification.Replace("-", ".");
            var runIdentifierNorm = runIdentifier.Replace("-", ".");

            await ExportEnvVarRuntime(entryIdNorm, "CONDITION", entrySetup.Condition ? "true" : "false");
            await ExportEnvVarRuntime(entryIdNorm, "RUNS_ON", runsOn);
            await ExportEnvVarRuntime(entryIdNorm, "RUN_SCRIPT", entrySetup.RunnerOSSetup.RunScript);
            await ExportEnvVarRuntime(entryIdNorm, "CACHE_KEY", $"{cacheFamilyNorm}-{osName}-{entryIdNorm}-{cacheInvalidatorNorm}-{environmentNorm}-{runClassificationNorm}-{runIdentifierNorm}");
            await ExportEnvVarRuntime(entryIdNorm, "CACHE_RESTORE_KEY", $"{cacheFamilyNorm}-{osName}-{entryIdNorm}-{cacheInvalidatorNorm}-{environmentNorm}-{runClassificationNorm}-");
            await ExportEnvVarRuntime(entryIdNorm, "CACHE_MAIN_RESTORE_KEY", $"{cacheFamilyNorm}-{osName}-{entryIdNorm}-{cacheInvalidatorNorm}-{environmentNorm}-main-");
            await ExportEnvVarRuntime(entryIdNorm, "CHECKOUT_FETCH_DEPTH", entrySetup.CheckoutFetchDepth.ToString());
            await ExportEnvVarRuntime(entryIdNorm, "CHECKOUT_FETCH_TAGS", entrySetup.CheckoutFetchTags ? "true" : "false");
            await ExportEnvVarRuntime(entryIdNorm, "CHECKOUT_SUBMODULES", GetSubmoduleCheckoutType(entrySetup.CheckoutSubmodules));
        }

        var entries = new List<(string entryId, string cacheFamily)>();
        entries.AddRange(pipelinePreSetup.TestEntries.Select(i => (i, "test")));
        entries.AddRange(pipelinePreSetup.BuildEntries.Select(i => (i, "build")));
        entries.AddRange(pipelinePreSetup.PublishEntries.Select(i => (i, "publish")));
        foreach (var (entryId, cacheFamily) in entries)
        {
            await setupEntryEnv(entryId, cacheFamily);
        }

        Log.Information("NUKE_PRE_SETUP: {preSetup}", JsonSerializer.Serialize(pipelinePreSetup, JsonExtension.SnakeCaseNamingOptionIndented));
        AbsolutePath.Create(Environment.GetEnvironmentVariable("GITHUB_OUTPUT")).AppendAllText($"\nNUKE_PRE_SETUP={JsonSerializer.Serialize(pipelinePreSetup, JsonExtension.SnakeCaseNamingOption)}");
    }

    public async Task PreparePostSetup(AllEntry allEntry, PipelinePreSetup? pipelinePreSetup)
    {
        foreach (var entryDefinition in allEntry.RunEntryDefinitionMap.Values)
        {
            // success, failure, cancelled, or skipped
            string result = Environment.GetEnvironmentVariable("NUKE_RUN_RESULT_GITHUB_" + entryDefinition.Id.ToUpperInvariant()) ?? "";
            result = result.Replace("failure", "error");
            result = result.Replace("cancelled", "error");
            Environment.SetEnvironmentVariable("NUKE_RUN_RESULT_" + entryDefinition.Id.ToUpperInvariant(), result);
        }

        if (BaseNukeBuildHelpers.CommonArtifactsDownloadDirectory.DirectoryExists())
        {
            foreach (var artifact in BaseNukeBuildHelpers.CommonArtifactsDownloadDirectory.GetDirectories())
            {
                await artifact.CopyRecursively(BaseNukeBuildHelpers.CommonArtifactsDirectory);
            }
        }
    }

    public Task FinalizePostSetup(AllEntry allEntry, PipelinePreSetup? pipelinePreSetup)
    {
        return Task.CompletedTask;
    }

    public async Task PrepareEntryRun(AllEntry allEntry, PipelinePreSetup? pipelinePreSetup, Dictionary<string, IRunEntryDefinition> entriesToRunMap)
    {
        if (BaseNukeBuildHelpers.CommonArtifactsDownloadDirectory.DirectoryExists())
        {
            foreach (var artifact in BaseNukeBuildHelpers.CommonArtifactsDownloadDirectory.GetDirectories())
            {
                await artifact.CopyRecursively(BaseNukeBuildHelpers.CommonArtifactsDirectory);
            }
        }
    }

    public Task FinalizeEntryRun(AllEntry allEntry, PipelinePreSetup? pipelinePreSetup, Dictionary<string, IRunEntryDefinition> entriesToRunMap)
    {
        return Task.CompletedTask;
    }

    public async Task BuildWorkflow(BaseNukeBuildHelpers baseNukeBuildHelpers, AllEntry allEntry)
    {
        var pipelineName = await allEntry.WorkflowConfigEntryDefinition.GetName();
        var pipelinePreSetupOs = await allEntry.WorkflowConfigEntryDefinition.GetPreSetupRunnerOS();
        var pipelinePostSetupOs = await allEntry.WorkflowConfigEntryDefinition.GetPostSetupRunnerOS();

        Dictionary<string, object> workflow = new()
        {
            ["name"] = pipelineName,
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

        Dictionary<string, ITestEntryDefinition> preTestEntryDefinitionMap = [];
        Dictionary<string, ITestEntryDefinition> postTestEntryDefinitionMap = [];

        foreach (var entryDefinition in allEntry.TestEntryDefinitionMap.Values)
        {
            if (await entryDefinition.GetExecuteBeforeBuild())
            {
                preTestEntryDefinitionMap[entryDefinition.Id] = entryDefinition;
            }
            else
            {
                postTestEntryDefinitionMap[entryDefinition.Id] = entryDefinition;
            }
        }

        // ██████████████████████████████████████
        // ██████████████ Pre Setup █████████████
        // ██████████████████████████████████████
        List<string> needs = [];
        var preSetupJob = AddJob(workflow, "PRE_SETUP", "Pre Setup", pipelinePreSetupOs, timeoutMinutes: 10);
        AddJobStepCheckout(preSetupJob, 0, true, SubmoduleCheckoutType.Recursive);
        var nukePreSetup = AddJobStepNukeRun(preSetupJob, pipelinePreSetupOs, "PipelinePreSetup", id: "NUKE_RUN");
        AddJobOrStepEnvVar(nukePreSetup, "GITHUB_TOKEN", "${{ secrets.GITHUB_TOKEN }}");
        AddJobOutput(preSetupJob, "NUKE_PRE_SETUP", "NUKE_RUN", "NUKE_PRE_SETUP");
        AddJobOutput(preSetupJob, "NUKE_PRE_SETUP_OUTPUT_TEST_MATRIX", "NUKE_RUN", "NUKE_PRE_SETUP_OUTPUT_TEST_MATRIX");
        AddJobOutput(preSetupJob, "NUKE_PRE_SETUP_OUTPUT_BUILD_MATRIX", "NUKE_RUN", "NUKE_PRE_SETUP_OUTPUT_BUILD_MATRIX");
        AddJobOutput(preSetupJob, "NUKE_PRE_SETUP_OUTPUT_PUBLISH_MATRIX", "NUKE_RUN", "NUKE_PRE_SETUP_OUTPUT_PUBLISH_MATRIX");
        foreach (var entryDefinition in allEntry.RunEntryDefinitionMap.Values)
        {
            ImportEnvVarWorkflow(preSetupJob, entryDefinition.Id.ToUpperInvariant(), "CONDITION");
            ImportEnvVarWorkflow(preSetupJob, entryDefinition.Id.ToUpperInvariant(), "RUNS_ON");
            ImportEnvVarWorkflow(preSetupJob, entryDefinition.Id.ToUpperInvariant(), "RUN_SCRIPT");
            ImportEnvVarWorkflow(preSetupJob, entryDefinition.Id.ToUpperInvariant(), "CACHE_KEY");
            ImportEnvVarWorkflow(preSetupJob, entryDefinition.Id.ToUpperInvariant(), "CACHE_RESTORE_KEY");
            ImportEnvVarWorkflow(preSetupJob, entryDefinition.Id.ToUpperInvariant(), "CACHE_MAIN_RESTORE_KEY");
            ImportEnvVarWorkflow(preSetupJob, entryDefinition.Id.ToUpperInvariant(), "CHECKOUT_FETCH_DEPTH");
            ImportEnvVarWorkflow(preSetupJob, entryDefinition.Id.ToUpperInvariant(), "CHECKOUT_FETCH_TAGS");
            ImportEnvVarWorkflow(preSetupJob, entryDefinition.Id.ToUpperInvariant(), "CHECKOUT_SUBMODULES");
        }
        needs.Add("PRE_SETUP");

        // ██████████████████████████████████████
        // ██████████████ Pre Test ██████████████
        // ██████████████████████████████████████
        List<string> preTestNeeds = [.. needs];
        foreach (var entryDefinition in preTestEntryDefinitionMap.Values)
        {
            IGithubWorkflowBuilder workflowBuilder = new GithubWorkflowBuilder();
            await entryDefinition.GetWorkflowBuilder(workflowBuilder);
            var testJob = AddJob(workflow, entryDefinition.Id.ToUpperInvariant(), await entryDefinition.GetDisplayName(workflowBuilder), GetImportedEnvVarFromJsonExpression(entryDefinition.Id.ToUpperInvariant(), "RUNS_ON"), needs: [.. needs], _if: "! failure() && ! cancelled() && " + GetImportedEnvVarName(entryDefinition.Id.ToUpperInvariant(), "CONDITION") + " == 'true'");
            AddJobOrStepEnvVarFromNeeds(testJob, "NUKE_PRE_SETUP", "PRE_SETUP");
            AddJobStepCheckout(testJob, entryDefinition.Id.ToUpperInvariant());
            AddJobStepNukeDefined(testJob, workflowBuilder, entryDefinition, "test");
            preTestNeeds.Add(entryDefinition.Id.ToUpperInvariant());
        }

        // ██████████████████████████████████████
        // ███████████████ Build ████████████████
        // ██████████████████████████████████████
        List<string> buildNeeds = [.. needs];
        foreach (var entryDefinition in allEntry.BuildEntryDefinitionMap.Values)
        {
            IGithubWorkflowBuilder workflowBuilder = new GithubWorkflowBuilder();
            await entryDefinition.GetWorkflowBuilder(workflowBuilder);
            string condition = "! failure() && ! cancelled() && " + GetImportedEnvVarName(entryDefinition.Id.ToUpperInvariant(), "CONDITION") + " == 'true'";
            foreach (var testEntryDefinition in preTestEntryDefinitionMap.Values)
            {
                if (testEntryDefinition.AppIds.Count == 0 || testEntryDefinition.AppIds.Any(i => i.Equals(entryDefinition.AppId, StringComparison.InvariantCultureIgnoreCase)))
                {
                    condition += " && needs." + testEntryDefinition.Id.ToUpperInvariant() + ".result != 'failure'";
                }
            }
            var buildJob = AddJob(workflow, entryDefinition.Id.ToUpperInvariant(), await entryDefinition.GetDisplayName(workflowBuilder), GetImportedEnvVarFromJsonExpression(entryDefinition.Id.ToUpperInvariant(), "RUNS_ON"), needs: [.. preTestNeeds], _if: condition);
            AddJobOrStepEnvVarFromNeeds(buildJob, "NUKE_PRE_SETUP", "PRE_SETUP");
            AddJobStepCheckout(buildJob, entryDefinition.Id.ToUpperInvariant());
            AddJobStepNukeDefined(buildJob, workflowBuilder, entryDefinition, "build");
            var uploadBuildStep = AddJobStep(buildJob, name: "Upload Artifacts", uses: "actions/upload-artifact@v4");
            AddJobStepWith(uploadBuildStep, "name", entryDefinition.AppId.NotNullOrEmpty().ToLowerInvariant() + artifactNameSeparator + entryDefinition.Id.ToUpperInvariant());
            AddJobStepWith(uploadBuildStep, "path", "./.nuke/temp/artifacts/*");
            AddJobStepWith(uploadBuildStep, "if-no-files-found", "error");
            AddJobStepWith(uploadBuildStep, "retention-days", "1");
            buildNeeds.Add(entryDefinition.Id.ToUpperInvariant());
        }

        // ██████████████████████████████████████
        // █████████████ Post Test ██████████████
        // ██████████████████████████████████████
        List<string> postTestNeeds = [.. needs];
        foreach (var entryDefinition in allEntry.TestEntryDefinitionMap.Values)
        {
            if (!await entryDefinition.GetExecuteBeforeBuild())
            {
                IGithubWorkflowBuilder workflowBuilder = new GithubWorkflowBuilder();
                await entryDefinition.GetWorkflowBuilder(workflowBuilder);
                string condition = "! failure() && ! cancelled() && " + GetImportedEnvVarName(entryDefinition.Id.ToUpperInvariant(), "CONDITION") + " == 'true'";
                foreach (var testEntryDefinition in preTestEntryDefinitionMap.Values)
                {
                    if (testEntryDefinition.AppIds.Count == 0 || testEntryDefinition.AppIds.Any(i => entryDefinition.AppIds.Any(j => i.Equals(j, StringComparison.InvariantCultureIgnoreCase))))
                    {
                        condition += " && needs." + testEntryDefinition.Id.ToUpperInvariant() + ".result != 'failure'";
                    }
                }
                foreach (var buildEntryDefinition in allEntry.BuildEntryDefinitionMap.Values)
                {
                    if (entryDefinition.AppIds.Any(i => i.Equals(buildEntryDefinition.AppId, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        condition += " && needs." + buildEntryDefinition.Id.ToUpperInvariant() + ".result != 'failure'";
                    }
                }
                var testJob = AddJob(workflow, entryDefinition.Id.ToUpperInvariant(), await entryDefinition.GetDisplayName(workflowBuilder), GetImportedEnvVarFromJsonExpression(entryDefinition.Id.ToUpperInvariant(), "RUNS_ON"), needs: [.. buildNeeds], _if: condition);
                AddJobOrStepEnvVarFromNeeds(testJob, "NUKE_PRE_SETUP", "PRE_SETUP");
                AddJobStepCheckout(testJob, entryDefinition.Id.ToUpperInvariant());
                var downloadPostTestStep = AddJobStep(testJob, name: "Download Artifacts", uses: "actions/download-artifact@v4");
                AddJobStepWith(downloadPostTestStep, "path", "./.nuke/temp/artifacts-download");
                AddJobStepNukeDefined(testJob, workflowBuilder, entryDefinition, "test");
                preTestNeeds.Add(entryDefinition.Id.ToUpperInvariant());
            }
        }

        // ██████████████████████████████████████
        // ██████████████ Publish ███████████████
        // ██████████████████████████████████████
        List<string> publishNeeds = [.. needs];
        foreach (var entryDefinition in allEntry.PublishEntryDefinitionMap.Values)
        {
            IGithubWorkflowBuilder workflowBuilder = new GithubWorkflowBuilder();
            await entryDefinition.GetWorkflowBuilder(workflowBuilder);
            string condition = "! failure() && ! cancelled() && " + GetImportedEnvVarName(entryDefinition.Id.ToUpperInvariant(), "CONDITION") + " == 'true'";
            foreach (var testEntryDefinition in allEntry.TestEntryDefinitionMap.Values)
            {
                if (testEntryDefinition.AppIds.Count == 0 || testEntryDefinition.AppIds.Any(i => i.Equals(entryDefinition.AppId, StringComparison.InvariantCultureIgnoreCase)))
                {
                    condition += " && needs." + testEntryDefinition.Id.ToUpperInvariant() + ".result != 'failure'";
                }
            }
            foreach (var buildEntryDefinition in allEntry.BuildEntryDefinitionMap.Values)
            {
                if (buildEntryDefinition.AppId.NotNullOrEmpty().Equals(entryDefinition.AppId, StringComparison.InvariantCultureIgnoreCase))
                {
                    condition += " && needs." + buildEntryDefinition.Id.ToUpperInvariant() + ".result != 'failure'";
                }
            }
            var publishJob = AddJob(workflow, entryDefinition.Id.ToUpperInvariant(), await entryDefinition.GetDisplayName(workflowBuilder), GetImportedEnvVarFromJsonExpression(entryDefinition.Id.ToUpperInvariant(), "RUNS_ON"), needs: [.. postTestNeeds], _if: condition);
            AddJobOrStepEnvVarFromNeeds(publishJob, "NUKE_PRE_SETUP", "PRE_SETUP");
            AddJobStepCheckout(publishJob, entryDefinition.Id.ToUpperInvariant());
            var downloadBuildStep = AddJobStep(publishJob, name: "Download artifacts", uses: "actions/download-artifact@v4");
            AddJobStepWith(downloadBuildStep, "path", "./.nuke/temp/artifacts-download");
            AddJobStepWith(downloadBuildStep, "pattern", entryDefinition.AppId.NotNullOrEmpty().ToLowerInvariant() + artifactNameSeparator + "*");
            AddJobStepNukeDefined(publishJob, workflowBuilder, entryDefinition, "publish");
            publishNeeds.Add(entryDefinition.Id.ToUpperInvariant());
        }

        // ██████████████████████████████████████
        // █████████████ Post Setup █████████████
        // ██████████████████████████████████████
        List<string> postNeeds = [.. needs];
        postNeeds.AddRange(preTestNeeds.Where(i => !needs.Contains(i)));
        postNeeds.AddRange(buildNeeds.Where(i => !needs.Contains(i)));
        postNeeds.AddRange(postTestNeeds.Where(i => !needs.Contains(i)));
        postNeeds.AddRange(publishNeeds.Where(i => !needs.Contains(i)));
        var postSetupJob = AddJob(workflow, "POST_SETUP", $"Post Setup", pipelinePostSetupOs, timeoutMinutes: 10, needs: [.. postNeeds], _if: "success() || failure() || always()");
        AddJobOrStepEnvVarFromNeeds(postSetupJob, "NUKE_PRE_SETUP", "PRE_SETUP");
        foreach (var entryDefinition in allEntry.RunEntryDefinitionMap.Values)
        {
            AddJobOrStepEnvVar(postSetupJob, "NUKE_RUN_RESULT_GITHUB_" + entryDefinition.Id.ToUpperInvariant(), $"${{{{ needs.{entryDefinition.Id.ToUpperInvariant()}.result }}}}");
        }
        AddJobStepCheckout(postSetupJob, 0, true, SubmoduleCheckoutType.Recursive);
        var downloadPostSetupStep = AddJobStep(postSetupJob, name: "Download Artifacts", uses: "actions/download-artifact@v4");
        AddJobStepWith(downloadPostSetupStep, "path", "./.nuke/temp/artifacts-download");
        var nukePostSetup = AddJobStepNukeRun(postSetupJob, pipelinePostSetupOs, "PipelinePostSetup");
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

    private static Task ExportEnvVarRuntime(string entryId, string name, string? value)
    {
        return Task.Run(() =>
        {
            AbsolutePath.Create(Environment.GetEnvironmentVariable("GITHUB_OUTPUT")).AppendAllText($"\nNUKE_PRE_SETUP_{entryId}_{name}={value}");
        });
    }

    private static void ImportEnvVarWorkflow(Dictionary<string, object> job, string entryId, string name)
    {
        AddJobOutput(job, $"NUKE_PRE_SETUP_{entryId}_{name}", "NUKE_RUN", $"NUKE_PRE_SETUP_{entryId}_{name}");
    }

    private static string GetImportedEnvVarName(string entryId, string name)
    {
        return "needs.PRE_SETUP.outputs.NUKE_PRE_SETUP_" + entryId + "_" + name;
    }

    private static string GetImportedEnvVarExpression(string entryId, string name)
    {
        return "${{ " + GetImportedEnvVarName(entryId, name) + " }}";
    }

    private static string GetImportedEnvVarFromJsonExpression(string entryId, string name)
    {
        return "${{ fromJson(" + GetImportedEnvVarName(entryId, name) + ") }}";
    }

    private static Dictionary<string, object> AddJob(Dictionary<string, object> workflow, string id, string name, object runsOn, int? timeoutMinutes = null, IEnumerable<string>? needs = null, string _if = "")
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
        if (timeoutMinutes != null)
        {
            job["timeout-minutes"] = timeoutMinutes.Value;
        }
        ((Dictionary<string, object>)workflow["jobs"])[id] = job;
        return job;
    }

    private static Dictionary<string, object> AddJob(Dictionary<string, object> workflow, string id, string name, RunnerOS runnerOS, int? timeoutMinutes = null, IEnumerable<string>? needs = null, string _if = "")
    {
        RunnerGithubPipelineOS runnerPipelineOS = (runnerOS.GetPipelineOS(PipelineType.Github) as RunnerGithubPipelineOS)!;
        return AddJob(workflow, id, name, ResolveRunsOn(runnerPipelineOS, true), timeoutMinutes, needs, _if);
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

    private static void AddJobStepNukeDefined(Dictionary<string, object> job, IGithubWorkflowBuilder workflowBuilder, IRunEntryDefinition entryDefinition, string runType)
    {
        AddJobStepCache(job, entryDefinition.Id.ToUpperInvariant());
        foreach (var step in workflowBuilder.PreExecuteSteps)
        {
            ((List<object>)job["steps"]).Add(step);
        }
        AddJobStepNukeRun(job, GetImportedEnvVarExpression(entryDefinition.Id.ToUpperInvariant(), "RUN_SCRIPT"), "PipelineRunEntry", id: "NUKE_RUN", args: $"run={runType};idsToRun={entryDefinition.Id}");
        foreach (var step in workflowBuilder.PostExecuteSteps)
        {
            ((List<object>)job["steps"]).Add(step);
        }
    }

    private static Dictionary<string, object> AddJobStepCheckout(Dictionary<string, object> job, int fetchDepth, bool fetchTags, SubmoduleCheckoutType submoduleCheckoutType, string _if = "")
    {
        var step = AddJobStep(job, uses: "actions/checkout@v4", _if: _if);
        AddJobStepWith(step, "fetch-depth", fetchDepth.ToString());
        AddJobStepWith(step, "fetch-tags", fetchTags ? "true" : "false");
        AddJobStepWith(step, "submodules", GetSubmoduleCheckoutType(submoduleCheckoutType));
        AddJobStepWith(step, "persist-credentials", "true");

        return step;
    }

    private static Dictionary<string, object> AddJobStepCheckout(Dictionary<string, object> job, string entryId)
    {
        var step = AddJobStep(job, uses: "actions/checkout@v4");
        AddJobStepWith(step, "fetch-depth", GetImportedEnvVarExpression(entryId, "CHECKOUT_FETCH_DEPTH"));
        AddJobStepWith(step, "fetch-tags", GetImportedEnvVarExpression(entryId, "CHECKOUT_FETCH_TAGS"));
        AddJobStepWith(step, "submodules", GetImportedEnvVarExpression(entryId, "CHECKOUT_SUBMODULES"));
        AddJobStepWith(step, "persist-credentials", "true");

        return step;
    }

    private static Dictionary<string, object> AddJobStepCache(Dictionary<string, object> job, string entryId)
    {
        var step = AddJobStep(job, name: "Cache Run", uses: "actions/cache@v4");
        AddJobStepWith(step, "path", "./.nuke/temp/cache");
        AddJobStepWith(step, "key", $"""
            {GetImportedEnvVarExpression(entryId, "CACHE_KEY")}
            """);
        AddJobStepWith(step, "restore-keys", $"""
            {GetImportedEnvVarExpression(entryId, "CACHE_RESTORE_KEY")}
            {GetImportedEnvVarExpression(entryId, "CACHE_MAIN_RESTORE_KEY")}
            """);
        return step;
    }

    private static Dictionary<string, object> AddJobStepNukeRun(Dictionary<string, object> job, string buildScript, string targetName, string id = "", string args = "", string _if = "")
    {
        var script = $"{buildScript} {targetName}";
        if (!string.IsNullOrEmpty(args))
        {
            script += $" --args \"{args}\"";
        }
        return AddJobStep(job, id: id, name: $"Run Nuke {targetName}", run: script, _if: _if);
    }

    private static Dictionary<string, object> AddJobStepNukeRun(Dictionary<string, object> job, RunnerOS runnerOS, string targetName, string id = "", string args = "", string _if = "")
    {
        return AddJobStepNukeRun(job, runnerOS.GetRunScript(PipelineType.Github), targetName, id, args, _if);
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

    private static string GetSubmoduleCheckoutType(SubmoduleCheckoutType submoduleCheckoutType)
    {
        return submoduleCheckoutType switch
        {
            SubmoduleCheckoutType.None => "false",
            SubmoduleCheckoutType.SingleLevel => "true",
            SubmoduleCheckoutType.Recursive => "recursive",
            _ => throw new NotImplementedException(submoduleCheckoutType.ToString())
        };
    }

    private static object ResolveRunsOn(RunnerGithubPipelineOS runnerPipelineOS, bool asDictionary)
    {
        Dictionary<string, object> runsOnObj = [];

        if (!string.IsNullOrEmpty(runnerPipelineOS.RunsOn))
        {
            runsOnObj = new Dictionary<string, object>()
            {
                ["labels"] = runnerPipelineOS.RunsOn
            };
        }
        else if (runnerPipelineOS.RunsOnLabels != null && runnerPipelineOS.RunsOnLabels.Length != 0 && !string.IsNullOrEmpty(runnerPipelineOS.Group))
        {
            runsOnObj = new Dictionary<string, object>()
            {
                ["labels"] = runnerPipelineOS.RunsOnLabels,
                ["group"] = runnerPipelineOS.Group,
            };
        }
        else if (runnerPipelineOS.RunsOnLabels != null && runnerPipelineOS.RunsOnLabels.Length != 0 && string.IsNullOrEmpty(runnerPipelineOS.Group))
        {
            runsOnObj = new Dictionary<string, object>()
            {
                ["labels"] = runnerPipelineOS.RunsOnLabels
            };
        }
        else if ((runnerPipelineOS.RunsOnLabels == null || runnerPipelineOS.RunsOnLabels.Length == 0) && !string.IsNullOrEmpty(runnerPipelineOS.Group))
        {
            runsOnObj = new Dictionary<string, object>()
            {
                ["group"] = runnerPipelineOS.Group
            };
        }

        if (asDictionary)
        {
            return runsOnObj;
        }
        else
        {
            return JsonSerializer.Serialize(runsOnObj);
        }
    }
}
