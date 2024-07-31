using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Entry.Definitions;
using NukeBuildHelpers.Entry.Enums;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.Pipelines.Azure.Interfaces;
using NukeBuildHelpers.Pipelines.Azure.Models;
using NukeBuildHelpers.Pipelines.Common.Enums;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.Pipelines.Common.Models;
using NukeBuildHelpers.Pipelines.Github.Models;
using NukeBuildHelpers.Runner.Abstraction;
using Serilog;
using System;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace NukeBuildHelpers.Pipelines.Azure;

internal class AzurePipeline(BaseNukeBuildHelpers nukeBuild) : IPipeline
{
    private readonly string artifactNameSeparator = "___";

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

            RunnerAzurePipelineOS runnerPipelineOS = JsonSerializer.Deserialize<RunnerAzurePipelineOS>(entrySetup.RunnerOSSetup.RunnerPipelineOS, JsonExtension.SnakeCaseNamingOptionIndented)!;

            var osName = entrySetup.RunnerOSSetup.Name.Replace("-", ".");
            var entryIdNorm = entryId.Replace("-", ".");
            var environmentNorm = pipelinePreSetup.Environment.Replace("-", ".");
            var cacheFamilyNorm  = cacheFamily.Replace("-", ".");
            var cacheInvalidatorNorm = entrySetup.CacheInvalidator.Replace("-", ".");
            var runClassificationNorm = runClassification.Replace("-", ".");
            var runIdentifierNorm = runIdentifier.Replace("-", ".");

            await ExportPreSetupEnvVarRuntime(entryIdNorm, "CONDITION", entrySetup.Condition ? "true" : "false");
            await ExportPreSetupEnvVarRuntime(entryIdNorm, "POOL_NAME", runnerPipelineOS.PoolName);
            await ExportPreSetupEnvVarRuntime(entryIdNorm, "POOL_VM_IMAGE", runnerPipelineOS.PoolVMImage);
            await ExportPreSetupEnvVarRuntime(entryIdNorm, "RUN_SCRIPT", entrySetup.RunnerOSSetup.RunScript);
            await ExportPreSetupEnvVarRuntime(entryIdNorm, "CACHE_KEY", $"\"{cacheFamilyNorm}\" | \"{osName}\" | \"{entryIdNorm}\" | \"{cacheInvalidatorNorm}\" | \"{environmentNorm}\" | \"{runClassificationNorm}\" | \"{runIdentifierNorm}\"");
            await ExportPreSetupEnvVarRuntime(entryIdNorm, "CACHE_RESTORE_KEY", $"\"{cacheFamilyNorm}\" | \"{osName}\" | \"{entryIdNorm}\" | \"{cacheInvalidatorNorm}\" | \"{environmentNorm}\" | \"{runClassificationNorm}\"");
            await ExportPreSetupEnvVarRuntime(entryIdNorm, "CACHE_MAIN_RESTORE_KEY", $"\"{cacheFamilyNorm}\" | \"{osName}\" | \"{entryIdNorm}\" | \"{cacheInvalidatorNorm}\" | \"{environmentNorm}\" | \"main\"");
            await ExportPreSetupEnvVarRuntime(entryIdNorm, "CHECKOUT_FETCH_DEPTH", entrySetup.CheckoutFetchDepth.ToString());
            await ExportPreSetupEnvVarRuntime(entryIdNorm, "CHECKOUT_FETCH_TAGS", entrySetup.CheckoutFetchTags ? "true" : "false");
            await ExportPreSetupEnvVarRuntime(entryIdNorm, "CHECKOUT_SUBMODULES", GetSubmoduleCheckoutType(entrySetup.CheckoutSubmodules));
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
        await ExportEnvVarRuntime("NUKE_PRE_SETUP", JsonSerializer.Serialize(pipelinePreSetup, JsonExtension.SnakeCaseNamingOption));
    }

    public async Task PreparePostSetup(AllEntry allEntry, PipelinePreSetup? pipelinePreSetup)
    {
        foreach (var entryDefinition in allEntry.RunEntryDefinitionMap.Values)
        {
            // Succeeded|SucceededWithIssues|Skipped|Failed|Canceled
            string result = Environment.GetEnvironmentVariable("NUKE_RUN_RESULT_AZURE_" + entryDefinition.Id.ToUpperInvariant()) ?? "";
            result = result.Replace("Succeeded", "success");
            result = result.Replace("SucceededWithIssues", "error");
            result = result.Replace("Failed", "error");
            result = result.Replace("Canceled", "error");
            result = result.Replace("Skipped", "skipped");
            Environment.SetEnvironmentVariable("NUKE_RUN_RESULT_" + entryDefinition.Id.ToUpperInvariant(), result);
        }

        if (BaseNukeBuildHelpers.CommonArtifactsDownloadDirectory.DirectoryExists())
        {
            foreach (var artifact in BaseNukeBuildHelpers.CommonArtifactsDownloadDirectory.GetDirectories())
            {
                await artifact.CopyRecursively(BaseNukeBuildHelpers.CommonArtifactsDirectory);
            }
        }

        if (BaseNukeBuildHelpers.CommonArtifactsDirectory.DirectoryExists())
        {
            foreach (var artifact in BaseNukeBuildHelpers.CommonArtifactsDirectory.GetDirectories())
            {
                var appId = artifact.Name.Split(artifactNameSeparator).FirstOrDefault().NotNullOrEmpty().ToLowerInvariant();
                await artifact.CopyRecursively(BaseNukeBuildHelpers.OutputDirectory / appId);
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

        // ██████████████████████████████████████
        // ██████████████ Pre Setup █████████████
        // ██████████████████████████████████████
        List<string> needs = [];
        var preSetupJob = AddJob(workflow, "PRE_SETUP", "Pre Setup", pipelinePreSetupOs, timeoutMinutes: 10);
        AddJobStepCheckout(preSetupJob, 0, true, SubmoduleCheckoutType.Recursive);
        var nukePreSetupStep = AddJobStepNukeRun(preSetupJob, pipelinePreSetupOs, "PipelinePreSetup", name: "NUKE_RUN");
        AddStepEnvVar(nukePreSetupStep, "GITHUB_TOKEN", "$(GITHUB_TOKEN)");
        needs.Add("PRE_SETUP");

        // ██████████████████████████████████████
        // ████████████████ Test ████████████████
        // ██████████████████████████████████████
        List<string> testNeeds = [.. needs];
        foreach (var entryDefinition in allEntry.TestEntryDefinitionMap.Values)
        {
            IAzureWorkflowBuilder workflowBuilder = new AzureWorkflowBuilder();
            await entryDefinition.GetWorkflowBuilder(workflowBuilder);
            var testJob = AddJob(workflow, entryDefinition.Id.ToUpperInvariant(), await entryDefinition.GetDisplayName(workflowBuilder), GetImportedEnvVarExpression(entryDefinition.Id.ToUpperInvariant(), "POOL_NAME"), GetImportedEnvVarExpression(entryDefinition.Id.ToUpperInvariant(), "POOL_VM_IMAGE"), needs: [.. needs], condition: "and(not(or(failed(), canceled())), eq(variables." + GetImportedEnvVarName(entryDefinition.Id.ToUpperInvariant(), "CONDITION") + ", 'true'))");
            AddJobEnvVarFromNeeds(testJob, "PRE_SETUP", "NUKE_RUN", "NUKE_PRE_SETUP");
            AddJobEnvVarFromNeedsDefined(testJob, entryDefinition.Id.ToUpperInvariant());
            AddJobStepCheckout(testJob, entryDefinition.Id.ToUpperInvariant());
            AddJobStepNukeDefined(testJob, workflowBuilder, entryDefinition, "test");
            testNeeds.Add(entryDefinition.Id.ToUpperInvariant());
        }

        // ██████████████████████████████████████
        // ███████████████ Build ████████████████
        // ██████████████████████████████████████
        List<string> buildNeeds = [.. needs];
        foreach (var entryDefinition in allEntry.BuildEntryDefinitionMap.Values)
        {
            IAzureWorkflowBuilder workflowBuilder = new AzureWorkflowBuilder();
            await entryDefinition.GetWorkflowBuilder(workflowBuilder);
            string condition = "and(not(or(failed(), canceled())), eq(variables." + GetImportedEnvVarName(entryDefinition.Id.ToUpperInvariant(), "CONDITION") + ", 'true'))";
            foreach (var testEntryDefinition in allEntry.TestEntryDefinitionMap.Values)
            {
                if (testEntryDefinition.AppIds.Count == 0 || testEntryDefinition.AppIds.Any(i => i.Equals(entryDefinition.AppId, StringComparison.InvariantCultureIgnoreCase)))
                {
                    condition = "and(" + condition + ", " + "eq(dependencies." + testEntryDefinition.Id.ToUpperInvariant() + ".result, 'Succeeded')" + ")";
                }
            }
            var buildJob = AddJob(workflow, entryDefinition.Id.ToUpperInvariant(), await entryDefinition.GetDisplayName(workflowBuilder), GetImportedEnvVarExpression(entryDefinition.Id.ToUpperInvariant(), "POOL_NAME"), GetImportedEnvVarExpression(entryDefinition.Id.ToUpperInvariant(), "POOL_VM_IMAGE"), needs: [.. testNeeds], condition: condition);
            AddJobEnvVarFromNeeds(buildJob, "PRE_SETUP", "NUKE_RUN", "NUKE_PRE_SETUP");
            AddJobEnvVarFromNeedsDefined(buildJob, entryDefinition.Id.ToUpperInvariant());
            AddJobStepCheckout(buildJob, entryDefinition.Id.ToUpperInvariant());
            AddJobStepNukeDefined(buildJob, workflowBuilder, entryDefinition, "build");
            var uploadBuildStep = AddJobStep(buildJob, displayName: "Upload Artifacts", task: "PublishPipelineArtifact@1");
            AddJobStepInputs(uploadBuildStep, "artifact", entryDefinition.AppId.NotNullOrEmpty().ToLowerInvariant() + artifactNameSeparator + entryDefinition.Id);
            AddJobStepInputs(uploadBuildStep, "targetPath", "./.nuke/temp/output");
            AddJobStepInputs(uploadBuildStep, "continueOnError", "true");
            buildNeeds.Add(entryDefinition.Id.ToUpperInvariant());
        }

        // ██████████████████████████████████████
        // ██████████████ Publish ███████████████
        // ██████████████████████████████████████
        List<string> publishNeeds = [.. needs];
        foreach (var entryDefinition in allEntry.PublishEntryDefinitionMap.Values)
        {
            IAzureWorkflowBuilder workflowBuilder = new AzureWorkflowBuilder();
            await entryDefinition.GetWorkflowBuilder(workflowBuilder);
            string condition = "and(not(or(failed(), canceled())), eq(variables." + GetImportedEnvVarName(entryDefinition.Id.ToUpperInvariant(), "CONDITION") + ", 'true'))";
            foreach (var testEntryDefinition in allEntry.TestEntryDefinitionMap.Values)
            {
                if (testEntryDefinition.AppIds.Count == 0 || testEntryDefinition.AppIds.Any(i => i.Equals(entryDefinition.AppId, StringComparison.InvariantCultureIgnoreCase)))
                {
                    condition = "and(" + condition + ", " + "ne(dependencies." + testEntryDefinition.Id.ToUpperInvariant() + ".result, 'Failed')" + ")";
                }
            }
            foreach (var buildEntryDefinition in allEntry.BuildEntryDefinitionMap.Values)
            {
                if (buildEntryDefinition.AppId.NotNullOrEmpty().Equals(entryDefinition.AppId, StringComparison.InvariantCultureIgnoreCase))
                {
                    condition = "and(" + condition + ", " + "ne(dependencies." + buildEntryDefinition.Id.ToUpperInvariant() + ".result, 'Failed')" + ")";
                }
            }
            var publishJob = AddJob(workflow, entryDefinition.Id.ToUpperInvariant(), await entryDefinition.GetDisplayName(workflowBuilder), GetImportedEnvVarExpression(entryDefinition.Id.ToUpperInvariant(), "POOL_NAME"), GetImportedEnvVarExpression(entryDefinition.Id.ToUpperInvariant(), "POOL_VM_IMAGE"), needs: [.. buildNeeds], condition: condition);
            AddJobEnvVarFromNeeds(publishJob, "PRE_SETUP", "NUKE_RUN", "NUKE_PRE_SETUP");
            AddJobEnvVarFromNeedsDefined(publishJob, entryDefinition.Id.ToUpperInvariant());
            AddJobStepCheckout(publishJob, entryDefinition.Id.ToUpperInvariant());
            var downloadPublishStep = AddJobStep(publishJob, displayName: "Download Artifacts", task: "DownloadPipelineArtifact@2");
            AddJobStepInputs(downloadPublishStep, "itemPattern", entryDefinition.AppId.NotNullOrEmpty().ToLowerInvariant() + artifactNameSeparator + "*/**");
            AddJobStepInputs(downloadPublishStep, "path", "./.nuke/temp/artifacts-download");
            AddJobStepInputs(downloadPublishStep, "continueOnError", "true");
            AddJobStepNukeDefined(publishJob, workflowBuilder, entryDefinition, "publish");
            publishNeeds.Add(entryDefinition.Id.ToUpperInvariant());
        }

        // ██████████████████████████████████████
        // █████████████ Post Setup █████████████
        // ██████████████████████████████████████
        List<string> postNeeds = [.. needs];
        postNeeds.AddRange(testNeeds.Where(i => !needs.Contains(i)));
        postNeeds.AddRange(buildNeeds.Where(i => !needs.Contains(i)));
        postNeeds.AddRange(publishNeeds.Where(i => !needs.Contains(i)));
        var postSetupJob = AddJob(workflow, "POST_SETUP", $"Post Setup", pipelinePostSetupOs, timeoutMinutes: 10, needs: [.. postNeeds], condition: "always()");
        AddJobEnvVarFromNeeds(postSetupJob, "PRE_SETUP", "NUKE_RUN", "NUKE_PRE_SETUP");
        foreach (var entryDefinition in allEntry.RunEntryDefinitionMap.Values)
        {
            AddJobEnvVar(postSetupJob, "NUKE_RUN_RESULT_AZURE_" + entryDefinition.Id.ToUpperInvariant(), $"$[ dependencies.{entryDefinition.Id.ToUpperInvariant()}.result ]");
        }
        AddJobStepCheckout(postSetupJob, 0, true, SubmoduleCheckoutType.Recursive);
        var downloadPostSetupStep = AddJobStep(postSetupJob, displayName: "Download artifacts", task: "DownloadPipelineArtifact@2");
        AddJobStepInputs(downloadPostSetupStep, "path", "./.nuke/temp/artifacts-download");
        AddJobStepInputs(downloadPostSetupStep, "patterns", "**");
        AddJobStepInputs(downloadPostSetupStep, "continueOnError", "true");
        var nukePostSetupStep = AddJobStepNukeRun(postSetupJob, pipelinePostSetupOs, "PipelinePostSetup");
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

    private static void AddJobEnvVarFromNeedsDefined(Dictionary<string, object> job, string entryId)
    {
        foreach (var varName in new string[]
        {
            "CONDITION",
            "POOL_NAME",
            "POOL_VM_IMAGE",
            "RUN_SCRIPT",
            "CACHE_KEY",
            "CACHE_RESTORE_KEY",
            "CACHE_MAIN_RESTORE_KEY",
            "CHECKOUT_FETCH_DEPTH",
            "CHECKOUT_FETCH_TAGS",
            "CHECKOUT_SUBMODULES",
        })
        {
            AddJobEnvVar(job, GetImportedEnvVarName(entryId, varName), $"$[ dependencies.PRE_SETUP.outputs['NUKE_RUN.{GetImportedEnvVarName(entryId, varName)}'] ]");
        }
    }

    private static Task ExportEnvVarRuntime(string name, string? value)
    {
        return Task.Run(() =>
        {
            Console.WriteLine($"##vso[task.setvariable variable={name}]{value}");
            Console.WriteLine($"##vso[task.setvariable variable={name};isOutput=true]{value}");
        });
    }

    private static Task ExportPreSetupEnvVarRuntime(string entryId, string name, string? value)
    {
        return ExportEnvVarRuntime(GetImportedEnvVarName(entryId, name), value);
    }

    private static string GetImportedEnvVarName(string entryId, string name)
    {
        return "NUKE_PRE_SETUP_" + entryId + "_" + name;
    }

    private static string GetImportedEnvVarExpression(string entryId, string name)
    {
        return "$(" + GetImportedEnvVarName(entryId, name) + ")";
    }

    private static Dictionary<string, object> AddJob(Dictionary<string, object> workflow, string id, string name, string? poolName, string? poolVMImage, int? timeoutMinutes = null, IEnumerable<string>? needs = null, string condition = "")
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
        if (timeoutMinutes != null)
        {
            job["timeoutInMinutes"] = timeoutMinutes.Value;
        }
        ((List<object>)workflow["jobs"]).Add(job);
        return job;
    }

    private static Dictionary<string, object> AddJob(Dictionary<string, object> workflow, string id, string name, RunnerOS runnerOS, int? timeoutMinutes = null, IEnumerable<string>? needs = null, string condition = "")
    {
        var azureRunnerOs = (runnerOS.GetPipelineOS(PipelineType.Azure) as RunnerAzurePipelineOS)!;
        return AddJob(workflow, id, name, azureRunnerOs.PoolName, azureRunnerOs.PoolVMImage, timeoutMinutes, needs, condition);
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

    private static void AddJobStepNukeDefined(Dictionary<string, object> job, IAzureWorkflowBuilder workflowBuilder, IRunEntryDefinition entryDefinition, string runType)
    {
        AddJobStepCache(job, entryDefinition.Id.ToUpperInvariant());
        foreach (var step in workflowBuilder.PreExecuteSteps)
        {
            ((List<object>)job["steps"]).Add(step);
        }
        AddJobStepNukeRun(job, GetImportedEnvVarExpression(entryDefinition.Id.ToUpperInvariant(), "RUN_SCRIPT"), "PipelineRunEntry", name: "NUKE_RUN", args: $"run={runType};idsToRun={entryDefinition.Id}");
        foreach (var step in workflowBuilder.PostExecuteSteps)
        {
            ((List<object>)job["steps"]).Add(step);
        }
    }

    private static Dictionary<string, object> AddJobStepCache(Dictionary<string, object> job, string entryId)
    {
        var step = AddJobStep(job, displayName: "Cache Run", task: "Cache@2");
        AddJobStepInputs(step, "path", "./.nuke/temp/cache");
        AddJobStepInputs(step, "key", $"""
            {GetImportedEnvVarExpression(entryId, "CACHE_KEY")}
            """);
        AddJobStepInputs(step, "restoreKeys", $"""
            {GetImportedEnvVarExpression(entryId, "CACHE_RESTORE_KEY")}
            {GetImportedEnvVarExpression(entryId, "CACHE_MAIN_RESTORE_KEY")}
            """);
        return step;
    }

    private static Dictionary<string, object> AddJobStepCheckout(Dictionary<string, object> job, int fetchDepth, bool fetchTags, SubmoduleCheckoutType submoduleCheckoutType, string condition = "")
    {
        Dictionary<string, object> step = AddJobStep(job);
        step["checkout"] = "self";
        step["fetchDepth"] = fetchDepth.ToString();
        step["fetchTags"] = fetchTags ? "true" : "false";
        step["submodules"] = GetSubmoduleCheckoutType(submoduleCheckoutType);
        if (!string.IsNullOrEmpty(condition))
        {
            step["condition"] = condition;
        }
        return step;
    }

    private static Dictionary<string, object> AddJobStepCheckout(Dictionary<string, object> job, string entryId)
    {
        Dictionary<string, object> step = AddJobStep(job);
        step["checkout"] = "self";
        step["fetchDepth"] = GetImportedEnvVarExpression(entryId, "CHECKOUT_FETCH_DEPTH");
        step["fetchTags"] = GetImportedEnvVarExpression(entryId, "CHECKOUT_FETCH_TAGS");
        step["submodules"] = GetImportedEnvVarExpression(entryId, "CHECKOUT_SUBMODULES");

        return step;
    }

    private static Dictionary<string, object> AddJobStepNukeRun(Dictionary<string, object> job, string buildScript, string targetName, string name = "", string args = "", string condition = "")
    {
        var script = $"{buildScript} {targetName}";
        if (!string.IsNullOrEmpty(args))
        {
            script += $" --args \"{args}\"";
        }
        return AddJobStep(job, displayName: $"Run Nuke {targetName}", name: name, script: script, condition: condition);
    }

    private static Dictionary<string, object> AddJobStepNukeRun(Dictionary<string, object> job, RunnerOS runnerOS, string targetName, string name = "", string args = "", string condition = "")
    {
        return AddJobStepNukeRun(job, runnerOS.GetRunScript(PipelineType.Azure), targetName, name, args, condition);
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

    private static void AddJobEnvVarFromNeeds(Dictionary<string, object> job, string needsId, string stepName, string envVarName)
    {
        AddJobEnvVar(job, envVarName, $"$[ dependencies.{needsId}.outputs['{stepName}.{envVarName}'] ]");
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
}
