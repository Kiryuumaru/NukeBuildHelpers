using NukeBuildHelpers.Entry.Models;

namespace NukeBuildHelpers.RunContext.Models;

public interface IVersionedContext : ICommitContext
{
    AppVersion AppVersion { get; }
}
