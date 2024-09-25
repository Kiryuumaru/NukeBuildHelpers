using NukeBuildHelpers.Common;
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

    internal Func<Task<bool>> AppendReleaseNotesAssetHashes { get; set; }

    internal Func<Task<bool>> EnablePrereleaseOnRelease { get; set; }

    internal Func<Task<long>> StartingBuildId { get; set; }

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

    internal async Task<bool> GetAppendReleaseNotesAssetHashes()
    {
        return await AppendReleaseNotesAssetHashes();
    }

    internal async Task<bool> GetEnablePrereleaseOnReleases()
    {
        return await EnablePrereleaseOnRelease();
    }

    internal async Task<long> GetStartingBuildId()
    {
        return await StartingBuildId();
    }
}
