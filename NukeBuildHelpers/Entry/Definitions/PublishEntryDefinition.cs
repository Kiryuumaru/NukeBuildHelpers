using Nuke.Common.IO;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;

namespace NukeBuildHelpers.Entry.Definitions;

internal class PublishEntryDefinition : TargetEntryDefinition, IPublishEntryDefinition
{
    List<Func<IRunContext, Task<AbsolutePath[]>>>? releaseAsset;
    List<Func<IRunContext, Task<AbsolutePath[]>>> IPublishEntryDefinition.ReleaseAsset
    {
        get => releaseAsset ?? [];
        set => releaseAsset = value;
    }

    List<Func<IRunContext, Task<AbsolutePath[]>>>? releaseCommonAsset;
    List<Func<IRunContext, Task<AbsolutePath[]>>> IPublishEntryDefinition.ReleaseCommonAsset
    {
        get => releaseCommonAsset ?? [];
        set => releaseCommonAsset = value;
    }

    protected override string GetDefaultName()
    {
        return "Publish - " + ((IPublishEntryDefinition)this).AppId + " (" + ((IPublishEntryDefinition)this).Id + ")";
    }

    protected override IRunEntryDefinition Clone()
    {
        var definition = new PublishEntryDefinition();
        FillClone(definition);
        return definition;
    }

    internal override void FillClone(IRunEntryDefinition definition)
    {
        base.FillClone(definition);
        if (releaseAsset != null) ((IPublishEntryDefinition)definition).ReleaseAsset = new List<Func<IRunContext, Task<AbsolutePath[]>>>(releaseAsset);
        if (releaseCommonAsset != null) ((IPublishEntryDefinition)definition).ReleaseCommonAsset = new List<Func<IRunContext, Task<AbsolutePath[]>>>(releaseCommonAsset);
    }
}
