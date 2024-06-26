using NukeBuildHelpers.Common.Enums;

namespace NukeBuildHelpers.RunContext.Interfaces;

public interface IRunContext
{
    RunType RunType { get; }
}
