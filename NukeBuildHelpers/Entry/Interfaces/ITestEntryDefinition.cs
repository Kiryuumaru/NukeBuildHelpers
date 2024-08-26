namespace NukeBuildHelpers.Entry.Interfaces;

/// <summary>
/// Interface defining a test-related entry in the build system.
/// </summary>
public interface ITestEntryDefinition : IDependentEntryDefinition
{
    internal Func<Task<bool>> ExecuteBeforeBuild { get; set; }

    internal async Task<bool> GetExecuteBeforeBuild()
    {
        return await ExecuteBeforeBuild();
    }
}
