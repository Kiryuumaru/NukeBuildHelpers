using NukeBuildHelpers.Common.Enums;

namespace NukeBuildHelpers.RunContext.Interfaces;

/// <summary>
/// Represents a context that includes run type information.
/// </summary>
public interface IRunContext
{
    /// <summary>
    /// Gets the run type associated with the context.
    /// </summary>
    RunType RunType { get; }
}
