using NukeBuildHelpers.Common;

namespace NukeBuildHelpers;

public abstract class Entry : BaseHelper
{
    public virtual bool Enable { get; } = true;

    public virtual bool RunParallel { get; } = true;

    public virtual string Id
    {
        get
        {
            return GetType().Name.ToSnakeCase();
        }
    }

    public virtual string Name
    {
        get
        {
            return GetType().Name;
        }
    }
}
