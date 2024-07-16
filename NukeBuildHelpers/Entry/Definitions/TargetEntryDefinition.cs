using NukeBuildHelpers.Entry.Interfaces;

namespace NukeBuildHelpers.Entry.Definitions;

internal abstract class TargetEntryDefinition : RunEntryDefinition, ITargetEntryDefinition
{
    string? ITargetEntryDefinition.AppId { get; set; }

    internal override void FillClone(IRunEntryDefinition definition)
    {
        base.FillClone(definition);
        ((ITargetEntryDefinition)definition).AppId = ((ITargetEntryDefinition)this).AppId;
    }
}
