using Nuke.Common.IO;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.RunContext.Interfaces;

namespace NukeBuildHelpers.Entry.Interfaces;

/// <summary>
/// Interface defining a publish-related entry in the build system.
/// </summary>
public interface IPublishEntryDefinition : ITargetEntryDefinition
{
}
