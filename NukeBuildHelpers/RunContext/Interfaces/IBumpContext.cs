using NukeBuildHelpers.Entry.Models;

namespace NukeBuildHelpers.RunContext.Models;

public interface IBumpContext : IVersionedContext
{
    new BumpReleaseVersion AppVersion { get; }
}
