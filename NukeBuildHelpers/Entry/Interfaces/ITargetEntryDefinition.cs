namespace NukeBuildHelpers.Entry.Interfaces;

/// <summary>
/// Interface defining a target entry in the build system.
/// </summary>
public interface ITargetEntryDefinition : IRunEntryDefinition
{
    internal string? AppId { get; set; }
}
