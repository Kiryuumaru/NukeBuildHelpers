using Nuke.Common.IO;

namespace NukeBuildHelpers.Models.RunContext;

public class RunContext
{
    public required AbsolutePath OutputDirectory { get; init; }
}
