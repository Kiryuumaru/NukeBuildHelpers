namespace NukeBuildHelpers.Common.Enums;

/// <summary>
/// Represents the type of workflow strategy.
/// </summary>
[Flags]
public enum WorkflowStrategyType
{
    /// <summary>
    /// All same appId will run synchronously.
    /// </summary>
    SynchronousAppId = 0b01,

    /// <summary>
    /// All entry will run synchronously regardless of its appId.
    /// </summary>
    SynchronousEntryType = 0b10
}
