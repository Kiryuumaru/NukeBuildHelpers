using Nuke.Common.IO;
using NukeBuildHelpers.Common;

namespace NukeBuildHelpers;

/// <summary>
/// Represents a base entry with common properties and methods.
/// </summary>
public abstract class Entry : BaseHelper
{
    /// <summary>
    /// Gets a value indicating whether the entry is enabled.
    /// </summary>
    public virtual bool Enable { get; } = true;

    /// <summary>
    /// Gets the ID of the entry.
    /// </summary>
    public virtual string Id
    {
        get
        {
            return GetType().Name.ToSnakeCase();
        }
    }

    /// <summary>
    /// Gets the name of the entry.
    /// </summary>
    public virtual string Name
    {
        get
        {
            return GetType().Name;
        }
    }

    /// <summary>
    /// Gets the cache invalidator string.
    /// </summary>
    public virtual string CacheInvalidator => "0";

    /// <summary>
    /// Gets the cache paths associated with the entry.
    /// </summary>
    public virtual AbsolutePath[] CachePaths { get; } = [];
}
