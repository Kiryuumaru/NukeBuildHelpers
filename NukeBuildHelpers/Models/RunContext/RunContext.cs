using Nuke.Common.IO;

namespace NukeBuildHelpers.Models;

public class RunContext
{
    public required AbsolutePath OutputDirectory { get; init; }
}
