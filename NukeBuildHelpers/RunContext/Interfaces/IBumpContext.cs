using NukeBuildHelpers.Entry.Models;

namespace NukeBuildHelpers.RunContext.Interfaces;

/// <summary>
/// Represents a context that includes information specific to version bumps.
/// </summary>
public interface IBumpContext : IVersionedContext
{
    /// <summary>
    /// Gets the bump release version associated with the context.
    /// </summary>
    new BumpReleaseVersion AppVersion { get; }
}
