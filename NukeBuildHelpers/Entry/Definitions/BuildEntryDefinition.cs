using Nuke.Common.IO;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.RunContext.Extensions;
using NukeBuildHelpers.RunContext.Interfaces;

namespace NukeBuildHelpers.Entry.Definitions;

internal class BuildEntryDefinition : TargetEntryDefinition, IBuildEntryDefinition
{
    List<Func<IRunContext, Task<AbsolutePath[]>>> IBuildEntryDefinition.ReleaseAsset { get; } = [];

    List<Func<IRunContext, Task<AbsolutePath[]>>> IBuildEntryDefinition.CommonReleaseAsset { get; } = [];

    protected override string GetDefaultName()
    {
        return "Build - " + ((IBuildEntryDefinition)this).AppId + " (" + Id + ")";
    }

    protected override Task<bool> GetDefaultCondition(IRunContext runContext)
    {
        return Task.FromResult(runContext.RunType == Common.Enums.RunType.Bump);
    }
}
