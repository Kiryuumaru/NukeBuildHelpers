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

    protected override IRunEntryDefinition Clone()
    {
        var definition = new BuildEntryDefinition()
        {
            Id = Id
        };
        FillClone(definition);
        return definition;
    }

    internal override void FillClone(IRunEntryDefinition definition)
    {
        base.FillClone(definition);
        ((IBuildEntryDefinition)definition).ReleaseAsset = new List<Func<IRunContext, Task<AbsolutePath[]>>>(((IBuildEntryDefinition)this).ReleaseAsset);
        ((IBuildEntryDefinition)definition).CommonReleaseAsset = new List<Func<IRunContext, Task<AbsolutePath[]>>>(((IBuildEntryDefinition)this).CommonReleaseAsset);
    }
}
