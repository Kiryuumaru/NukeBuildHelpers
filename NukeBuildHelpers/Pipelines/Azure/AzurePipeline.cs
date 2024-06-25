using Nuke.Common;
using Nuke.Common.Utilities;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Entry.Definitions;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.Pipelines.Azure.Interfaces;
using NukeBuildHelpers.Pipelines.Azure.Models;
using NukeBuildHelpers.Pipelines.Common.Enums;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.Pipelines.Common.Models;
using NukeBuildHelpers.Runner.Abstraction;
using Serilog;
using System.Linq;
using System.Text.Json;

namespace NukeBuildHelpers.Pipelines.Azure;

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

    public async Task FinalizePreSetup(AllEntry allEntry, PipelinePreSetup pipelinePreSetup)
    {
        var runClassification = pipelinePreSetup.TriggerType == TriggerType.PullRequest ? "pr." + pipelinePreSetup.PullRequestNumber : "main";
        var runIdentifier = Guid.NewGuid().Encode();

        async Task ExportEnvVarEntryRuntime(string entryId, bool condition, string? poolName, string? poolVmImage, string runsScript, string cacheFamily, string osName, string cacheInvalidator, string environment)
        {
            cacheFamily = cacheFamily.Replace("-", ".");
            osName = osName.Replace("-", ".");
            entryId = entryId.Replace("-", ".");
            cacheInvalidator = cacheInvalidator.Replace("-", ".");
            environment = environment.Replace("-", ".");
            runClassification = runClassification.Replace("-", ".");
            runIdentifier = runIdentifier.Replace("-", ".");

            await ExportPreSetupEnvVarRuntime(entryId, "CONDITION", condition ? "true" : "false");
            await ExportPreSetupEnvVarRuntime(entryId, "POOL_NAME", poolName);
            await ExportPreSetupEnvVarRuntime(entryId, "POOL_VM_IMAGE", poolVmImage);
            await ExportPreSetupEnvVarRuntime(entryId, "RUN_SCRIPT", runsScript);
            await ExportPreSetupEnvVarRuntime(entryId, "CACHE_KEY", $"\"{cacheFamily}\" | \"{osName}\" | \"{entryId}\" | \"{cacheInvalidator}\" | \"{environment}\" | \"{runClassification}\" | \"{runIdentifier}\"");
            await ExportPreSetupEnvVarRuntime(entryId, "CACHE_RESTORE_KEY", $"\"{cacheFamily}\" | \"{osName}\" | \"{entryId}\" | \"{cacheInvalidator}\" | \"{environment}\" | \"{runClassification}\"");
            await ExportPreSetupEnvVarRuntime(entryId, "CACHE_MAIN_RESTORE_KEY", $"\"{cacheFamily}\" | \"{osName}\" | \"{entryId}\" | \"{cacheInvalidator}\" | \"{environment}\" | \"main\"");
        }

        foreach (var entryId in pipelinePreSetup.TestEntries)
        {
            if (!allEntry.TestEntryDefinitionMap.TryGetValue(entryId, out var appEntryDefinition))
            {
                continue;
            }
            if (!pipelinePreSetup.EntrySetupMap.TryGetValue(entryId, out var entrySetup))
            {
                continue;
            }

            RunnerAzurePipelineOS runnerPipelineOS = JsonSerializer.Deserialize<RunnerAzurePipelineOS>(entrySetup.RunnerOSSetup.RunnerPipelineOS, JsonExtension.SnakeCaseNamingOptionIndented)!;
            
            await ExportEnvVarEntryRuntime(entryId, entrySetup.Condition, runnerPipelineOS.PoolName, runnerPipelineOS.PoolVMImage, entrySetup.RunnerOSSetup.RunScript, "test", entrySetup.RunnerOSSetup.Name, entrySetup.CacheInvalidator, pipelinePreSetup.Environment);
        }

        foreach (var entryId in pipelinePreSetup.BuildEntries)
        {
            if (!pipelinePreSetup.EntrySetupMap.TryGetValue(entryId, out var entrySetup))
            {
                continue;
            }

            RunnerAzurePipelineOS runnerPipelineOS = JsonSerializer.Deserialize<RunnerAzurePipelineOS>(entrySetup.RunnerOSSetup.RunnerPipelineOS, JsonExtension.SnakeCaseNamingOptionIndented)!;
            
            await ExportEnvVarEntryRuntime(entryId, entrySetup.Condition, runnerPipelineOS.PoolName, runnerPipelineOS.PoolVMImage, entrySetup.RunnerOSSetup.RunScript, "build", entrySetup.RunnerOSSetup.Name, entrySetup.CacheInvalidator, pipelinePreSetup.Environment);
        }

        foreach (var entryId in pipelinePreSetup.PublishEntries)
        {
            if (!pipelinePreSetup.EntrySetupMap.TryGetValue(entryId, out var entrySetup))
            {
                continue;
            }

            RunnerAzurePipelineOS runnerPipelineOS = JsonSerializer.Deserialize<RunnerAzurePipelineOS>(entrySetup.RunnerOSSetup.RunnerPipelineOS, JsonExtension.SnakeCaseNamingOptionIndented)!;
            
            await ExportEnvVarEntryRuntime(entryId, entrySetup.Condition, runnerPipelineOS.PoolName, runnerPipelineOS.PoolVMImage, entrySetup.RunnerOSSetup.RunScript, "publish", entrySetup.RunnerOSSetup.Name, entrySetup.CacheInvalidator, pipelinePreSetup.Environment);
        }

        Log.Information("NUKE_PRE_SETUP: {preSetup}", JsonSerializer.Serialize(pipelinePreSetup, JsonExtension.SnakeCaseNamingOptionIndented));
        await ExportEnvVarRuntime("NUKE_PRE_SETUP", JsonSerializer.Serialize(pipelinePreSetup, JsonExtension.SnakeCaseNamingOption));
    }

    public Task PreparePostSetup(AllEntry allEntry, PipelinePreSetup pipelinePreSetup)
    {
        return Task.Run(() =>
        {
            foreach (var entryDefinition in allEntry.EntryDefinitionMap.Values)
            {
                string result = Environment.GetEnvironmentVariable("NUKE_RUN_RESULT_AZURE_" + entryDefinition.Id) ?? "";
                result = result.Replace("SucceededWithIssues", "error");
                result = result.Replace("Failed", "error");
                result = result.Replace("Canceled", "error");
                Environment.SetEnvironmentVariable("NUKE_RUN_RESULT_" + entryDefinition.Id, result);
            }
        });
    }

    public Task FinalizePostSetup(AllEntry allEntry, PipelinePreSetup pipelinePreSetup)
    {
        return Task.CompletedTask;
    }

    public Task PrepareEntryRun(AllEntry allEntry, PipelinePreSetup pipelinePreSetup)
    {
        return Task.CompletedTask;
    }

    public Task FinalizeEntryRun(AllEntry allEntry, PipelinePreSetup pipelinePreSetup)
    {
        return Task.CompletedTask;
    }

    public async Task BuildWorkflow(BaseNukeBuildHelpers baseNukeBuildHelpers, AllEntry allEntry)
    {
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

        // ██████████████████████████████████████
        // ██████████████ Pre Setup █████████████
        // ██████████████████████████████████████
        List<string> needs = [];
        var preSetupJob = AddJob(workflow, "pre_setup", "Pre Setup", RunnerOS.Ubuntu2204);
        AddJobStepCheckout(preSetupJob, fetchDepth: 0);
        var nukePreSetupStep = AddJobStepNukeRun(preSetupJob, RunnerOS.Ubuntu2204, "PipelinePreSetup", name: "NUKE_RUN");
        AddStepEnvVar(nukePreSetupStep, "GITHUB_TOKEN", "$(GITHUB_TOKEN)");
        needs.Add("pre_setup");

        // ██████████████████████████████████████
        // ████████████████ Test ████████████████
        // ██████████████████████████████████████
        List<string> testNeeds = [.. needs];
        foreach (var entryDefinition in allEntry.TestEntryDefinitionMap.Values)
        {
            IAzureWorkflowBuilder workflowBuilder = new AzureWorkflowBuilder();
            await entryDefinition.GetWorkflowBuilder(workflowBuilder);
            var testJob = AddJob(workflow, entryDefinition.Id, await entryDefinition.GetDisplayName(workflowBuilder), GetImportedEnvVarExpression("POOL_NAME"), GetImportedEnvVarExpression("POOL_VM_IMAGE"), needs: [.. needs], condition: "and(succeeded(), eq(variables." + GetImportedEnvVarName("CONDITION") + ", 'true'))");
            AddJobEnvVarFromNeeds(testJob, "pre_setup", "NUKE_RUN", "NUKE_PRE_SETUP");
            AddJobEnvVarFromNeedsDefined(testJob, entryDefinition.Id);
            AddJobStepCheckout(testJob);
            AddJobStepNukeDefined(testJob, workflowBuilder, entryDefinition, "PipelineTest");
            testNeeds.Add(entryDefinition.Id);
        }

        // ██████████████████████████████████████
        // ███████████████ Build ████████████████
        // ██████████████████████████████████████
        List<string> buildNeeds = [.. needs];
        foreach (var entryDefinition in allEntry.BuildEntryDefinitionMap.Values)
        {
            IAzureWorkflowBuilder workflowBuilder = new AzureWorkflowBuilder();
            await entryDefinition.GetWorkflowBuilder(workflowBuilder);
            var buildJob = AddJob(workflow, entryDefinition.Id, await entryDefinition.GetDisplayName(workflowBuilder), GetImportedEnvVarExpression("POOL_NAME"), GetImportedEnvVarExpression("POOL_VM_IMAGE"), needs: [.. testNeeds], condition: "and(succeeded(), eq(variables." + GetImportedEnvVarName("CONDITION") + ", 'true'))");
            AddJobEnvVarFromNeeds(buildJob, "pre_setup", "NUKE_RUN", "NUKE_PRE_SETUP");
            AddJobEnvVarFromNeedsDefined(buildJob, entryDefinition.Id);
            AddJobStepCheckout(buildJob);
            AddJobStepNukeDefined(buildJob, workflowBuilder, entryDefinition, "PipelineBuild");
            var uploadBuildStep = AddJobStep(buildJob, displayName: "Upload Artifacts", task: "PublishPipelineArtifact@1");
            AddJobStepInputs(uploadBuildStep, "artifact", entryDefinition.AppId + "-" + entryDefinition.Id);
            AddJobStepInputs(uploadBuildStep, "targetPath", "./.nuke/output");
            AddJobStepInputs(uploadBuildStep, "continueOnError", "true");
            buildNeeds.Add(entryDefinition.Id);
        }

        // ██████████████████████████████████████
        // ██████████████ Publish ███████████████
        // ██████████████████████████████████████
        List<string> publishNeeds = [.. needs];
        foreach (var entryDefinition in allEntry.PublishEntryDefinitionMap.Values)
        {
            IAzureWorkflowBuilder workflowBuilder = new AzureWorkflowBuilder();
            await entryDefinition.GetWorkflowBuilder(workflowBuilder);
            var publishJob = AddJob(workflow, entryDefinition.Id, await entryDefinition.GetDisplayName(workflowBuilder), GetImportedEnvVarExpression("POOL_NAME"), GetImportedEnvVarExpression("POOL_VM_IMAGE"), needs: [.. buildNeeds], condition: "and(succeeded(), eq(variables." + GetImportedEnvVarName("CONDITION") + ", 'true'))");
            AddJobEnvVarFromNeeds(publishJob, "pre_setup", "NUKE_RUN", "NUKE_PRE_SETUP");
            AddJobEnvVarFromNeedsDefined(publishJob, entryDefinition.Id);
            AddJobStepCheckout(publishJob);
            var downloadPublishStep = AddJobStep(publishJob, displayName: "Download Artifacts", task: "DownloadPipelineArtifact@2");
            AddJobStepInputs(downloadPublishStep, "itemPattern", entryDefinition.AppId + "-@(" + allEntry.BuildEntryDefinitionMap.Values.Where(i => i.AppId.NotNullOrEmpty().Equals(entryDefinition.AppId, StringComparison.InvariantCultureIgnoreCase)).Select(i => i.Id).Join("|") + ")");
            AddJobStepInputs(downloadPublishStep, "path", "./.nuke/output");
            AddJobStepInputs(downloadPublishStep, "continueOnError", "true");
            AddJobStepNukeDefined(publishJob, workflowBuilder, entryDefinition, "PipelinePublish");
            publishNeeds.Add(entryDefinition.Id);
        }

        // ██████████████████████████████████████
        // █████████████ Post Setup █████████████
        // ██████████████████████████████████████
        List<string> postNeeds = [.. needs];
        postNeeds.AddRange(testNeeds.Where(i => !needs.Contains(i)));
        postNeeds.AddRange(buildNeeds.Where(i => !needs.Contains(i)));
        postNeeds.AddRange(publishNeeds.Where(i => !needs.Contains(i)));
        var postSetupJob = AddJob(workflow, "post_setup", $"Post Setup", RunnerOS.Ubuntu2204, needs: [.. postNeeds], condition: "always()");
        AddJobEnvVarFromNeeds(postSetupJob, "pre_setup", "NUKE_RUN", "NUKE_PRE_SETUP");
        foreach (var entryDefinition in allEntry.EntryDefinitionMap.Values)
        {
            AddJobEnvVar(postSetupJob, "NUKE_RUN_RESULT_AZURE_" + entryDefinition.Id, $"$[ dependencies.{entryDefinition.Id}.result ]");
        }
        AddJobStepCheckout(postSetupJob);
        var downloadPostSetupStep = AddJobStep(postSetupJob, displayName: "Download artifacts", task: "DownloadPipelineArtifact@2");
        AddJobStepInputs(downloadPostSetupStep, "path", "./.nuke/output");
        AddJobStepInputs(downloadPostSetupStep, "patterns", "**");
        AddJobStepInputs(downloadPostSetupStep, "continueOnError", "true");
        var nukePostSetupStep = AddJobStepNukeRun(postSetupJob, RunnerOS.Ubuntu2204, "PipelinePostSetup");
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
        })
        {
            AddJobEnvVar(job, GetImportedEnvVarName(varName), $"$[ dependencies.pre_setup.outputs['NUKE_RUN.{GetImportedEnvVarName(entryId + "_" + varName)}'] ]");
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
        return ExportEnvVarRuntime(GetImportedEnvVarName($"{entryId}_{name}"), value);
    }

    private static string GetImportedEnvVarName(string name)
    {
        return "NUKE_PRE_SETUP_" + name;
    }

    private static string GetImportedEnvVarExpression(string name)
    {
        return "$(" + GetImportedEnvVarName(name) + ")";
    }

    private static Dictionary<string, object> AddJob(Dictionary<string, object> workflow, string id, string name, string? poolName, string? poolVMImage, IEnumerable<string>? needs = null, string condition = "")
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
        ((List<object>)workflow["jobs"]).Add(job);
        return job;
    }

    private static Dictionary<string, object> AddJob(Dictionary<string, object> workflow, string id, string name, RunnerOS runnerOS, IEnumerable<string>? needs = null, string condition = "")
    {
        var azureRunnerOs = (runnerOS.GetPipelineOS(PipelineType.Azure) as RunnerAzurePipelineOS)!;
        return AddJob(workflow, id, name, azureRunnerOs.PoolName, azureRunnerOs.PoolVMImage, needs, condition);
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

    private static void AddJobStepNukeDefined(Dictionary<string, object> job, IAzureWorkflowBuilder workflowBuilder, IEntryDefinition entryDefinition, string targetName)
    {
        AddJobStepCache(job);
        foreach (var step in workflowBuilder.PreExecuteSteps)
        {
            ((List<object>)job["steps"]).Add(step);
        }
        AddJobStepNukeRun(job, GetImportedEnvVarExpression("RUN_SCRIPT"), targetName, name: "NUKE_RUN", args: entryDefinition.Id);
        foreach (var step in workflowBuilder.PostExecuteSteps)
        {
            ((List<object>)job["steps"]).Add(step);
        }
    }

    private static Dictionary<string, object> AddJobStepCache(Dictionary<string, object> job)
    {
        var step = AddJobStep(job, displayName: "Cache Run", task: "Cache@2");
        AddJobStepInputs(step, "path", "./.nuke/cache");
        AddJobStepInputs(step, "key", $"""
            {GetImportedEnvVarExpression("CACHE_KEY")}
            """);
        AddJobStepInputs(step, "restoreKeys", $"""
            {GetImportedEnvVarExpression("CACHE_RESTORE_KEY")}
            {GetImportedEnvVarExpression("CACHE_MAIN_RESTORE_KEY")}
            """);
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
}
