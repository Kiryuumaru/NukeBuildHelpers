using NukeBuildHelpers.Entry.Interfaces;

namespace NukeBuildHelpers.Entry.Definitions;

internal abstract class TargetEntryDefinition : EntryDefinition, ITargetEntryDefinition
{
    string? ITargetEntryDefinition.AppId { get; set; }

    internal override void FillClone(IEntryDefinition definition)
    {
        base.FillClone(definition);
        ((ITargetEntryDefinition)definition).AppId = ((ITargetEntryDefinition)this).AppId;
    }
}
