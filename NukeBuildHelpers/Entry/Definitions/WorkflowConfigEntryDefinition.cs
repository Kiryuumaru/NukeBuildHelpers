using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;
using NukeBuildHelpers.Runner.Abstraction;

namespace NukeBuildHelpers.Entry.Definitions;

internal class WorkflowConfigEntryDefinition : IWorkflowConfigEntryDefinition
{
    Func<Task<string>>? name = null;
    Func<Task<string>> IWorkflowConfigEntryDefinition.Name
    {
        get => name ?? (() => Task.FromResult("Nuke CICD Pipeline"));
        set => name = value;
    }

    Func<Task<RunnerOS>>? preSetupRunnerOS = null;
    Func<Task<RunnerOS>> IWorkflowConfigEntryDefinition.PreSetupRunnerOS
    {
        get => preSetupRunnerOS ?? (() => Task.FromResult(RunnerOS.Ubuntu2204));
        set => preSetupRunnerOS = value;
    }

    Func<Task<RunnerOS>>? postSetupRunnerOS = null;
    Func<Task<RunnerOS>> IWorkflowConfigEntryDefinition.PostSetupRunnerOS
    {
        get => postSetupRunnerOS ?? (() => Task.FromResult(RunnerOS.Ubuntu2204));
        set => postSetupRunnerOS = value;
    }

    IWorkflowConfigEntryDefinition IWorkflowConfigEntryDefinition.Clone()
    {
        var definition = new WorkflowConfigEntryDefinition();
        if (name != null) ((IWorkflowConfigEntryDefinition)this).Name = name;
        if (preSetupRunnerOS != null) ((IWorkflowConfigEntryDefinition)this).PreSetupRunnerOS = preSetupRunnerOS;
        if (postSetupRunnerOS != null) ((IWorkflowConfigEntryDefinition)this).PostSetupRunnerOS = postSetupRunnerOS;
        return definition;
    }
}
