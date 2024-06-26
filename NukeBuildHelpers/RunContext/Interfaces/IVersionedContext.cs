using NukeBuildHelpers.Entry.Models;

namespace NukeBuildHelpers.RunContext.Interfaces;

/// <summary>
/// Represents a context that includes version information.
/// </summary>
public interface IVersionedContext : ICommitContext
{
    /// <summary>
    /// Gets the application version associated with the context.
    /// </summary>
    AppVersion AppVersion { get; }
}
