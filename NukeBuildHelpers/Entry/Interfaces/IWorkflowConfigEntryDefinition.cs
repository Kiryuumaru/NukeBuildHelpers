using Nuke.Common.IO;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Entry.Enums;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;
using NukeBuildHelpers.Runner.Abstraction;

namespace NukeBuildHelpers.Entry.Interfaces;

/// <summary>
/// Interface defining the workflow config definition of the build system.
/// </summary>
public interface IWorkflowConfigEntryDefinition
{
    internal IWorkflowConfigEntryDefinition Clone();

    internal Func<Task<string>> Name { get; set; }

    internal Func<Task<RunnerOS>> PreSetupRunnerOS { get; set; }

    internal Func<Task<RunnerOS>> PostSetupRunnerOS { get; set; }

    internal async Task<string> GetName()
    {
        return ValueHelpers.GetOrNullFail(await Name());
    }

    internal async Task<RunnerOS> GetPreSetupRunnerOS()
    {
        return ValueHelpers.GetOrNullFail(await PreSetupRunnerOS());
    }

    internal async Task<RunnerOS> GetPostSetupRunnerOS()
    {
        return ValueHelpers.GetOrNullFail(await PostSetupRunnerOS());
    }
}
