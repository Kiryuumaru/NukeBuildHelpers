using Nuke.Common.IO;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.RunContext.Extensions;
using NukeBuildHelpers.RunContext.Interfaces;

namespace NukeBuildHelpers.Entry.Definitions;

internal class BuildEntryDefinition : TargetEntryDefinition, IBuildEntryDefinition
{
    List<Func<IRunContext, Task<AbsolutePath[]>>>? releaseAsset;
    List<Func<IRunContext, Task<AbsolutePath[]>>> IBuildEntryDefinition.ReleaseAsset
    {
        get => releaseAsset ?? [];
        set => releaseAsset = value;
    }

    List<Func<IRunContext, Task<AbsolutePath[]>>>? commonReleaseAsset;
    List<Func<IRunContext, Task<AbsolutePath[]>>> IBuildEntryDefinition.CommonReleaseAsset
    {
        get => commonReleaseAsset ?? [];
        set => commonReleaseAsset = value;
    }

    protected override string GetDefaultName()
    {
        return "Build - " + ((IBuildEntryDefinition)this).AppId + " (" + ((IBuildEntryDefinition)this).Id + ")";
    }

    protected override IRunEntryDefinition Clone()
    {
        var definition = new BuildEntryDefinition();
        FillClone(definition);
        return definition;
    }

    internal override void FillClone(IRunEntryDefinition definition)
    {
        base.FillClone(definition);
        if (releaseAsset != null) ((IBuildEntryDefinition)definition).ReleaseAsset = new List<Func<IRunContext, Task<AbsolutePath[]>>>(releaseAsset);
        if (commonReleaseAsset != null) ((IBuildEntryDefinition)definition).CommonReleaseAsset = new List<Func<IRunContext, Task<AbsolutePath[]>>>(commonReleaseAsset);
    }
}
