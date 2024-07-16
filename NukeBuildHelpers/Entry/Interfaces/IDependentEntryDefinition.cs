namespace NukeBuildHelpers.Entry.Interfaces;

/// <summary>
/// Interface defining a dependent entry in the build system.
/// </summary>
public interface IDependentEntryDefinition : IRunEntryDefinition
{
    internal List<string> AppIds { get; set; }
}
