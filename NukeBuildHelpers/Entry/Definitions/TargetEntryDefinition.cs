using NukeBuildHelpers.Entry.Interfaces;

namespace NukeBuildHelpers.Entry.Definitions;

internal abstract class TargetEntryDefinition : RunEntryDefinition, ITargetEntryDefinition
{
    string? appId;
    string? ITargetEntryDefinition.AppId
    {
        get => appId;
        set => appId = value;
    }

    internal override void FillClone(IRunEntryDefinition definition)
    {
        base.FillClone(definition);
        if (appId != null) ((ITargetEntryDefinition)definition).AppId = appId;
    }
}
