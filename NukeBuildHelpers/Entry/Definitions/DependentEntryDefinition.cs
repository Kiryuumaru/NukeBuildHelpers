using NukeBuildHelpers.Entry.Extensions;
using NukeBuildHelpers.Entry.Interfaces;

namespace NukeBuildHelpers.Entry.Definitions;

internal abstract class DependentEntryDefinition : EntryDefinition, IDependentEntryDefinition
{
    string[] IDependentEntryDefinition.AppIds { get; set; } = [];

    internal override void FillClone(IEntryDefinition definition)
    {
        base.FillClone(definition);
        ((IDependentEntryDefinition)definition).AppIds = ((IDependentEntryDefinition)this).AppIds;
    }
}
