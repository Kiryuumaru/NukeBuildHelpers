using Nuke.Common.IO;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.RunContext.Extensions;
using NukeBuildHelpers.RunContext.Interfaces;

namespace NukeBuildHelpers.Entry.Definitions;

internal class BuildEntryDefinition : TargetEntryDefinition, IBuildEntryDefinition
{
    List<Func<IRunContext, Task<AbsolutePath[]>>> IBuildEntryDefinition.ReleaseAsset { get; set; } = [];

    List<Func<IRunContext, Task<AbsolutePath[]>>> IBuildEntryDefinition.CommonReleaseAsset { get; set; } = [];

    protected override string GetDefaultName()
    {
        return "Build - " + ((IBuildEntryDefinition)this).AppId + " (" + Id + ")";
    }

    protected override IEntryDefinition Clone()
    {
        var definition = new BuildEntryDefinition()
        {
            Id = Id
        };
        ((IBuildEntryDefinition)definition).ReleaseAsset = ((IBuildEntryDefinition)this).ReleaseAsset;
        ((IBuildEntryDefinition)definition).CommonReleaseAsset = ((IBuildEntryDefinition)this).CommonReleaseAsset;
        FillClone(definition);
        return definition;
    }
}
