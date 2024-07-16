using NukeBuildHelpers.Entry.Extensions;
using NukeBuildHelpers.Entry.Interfaces;

namespace NukeBuildHelpers.Entry.Definitions;

internal abstract class DependentEntryDefinition : RunEntryDefinition, IDependentEntryDefinition
{
    List<string> IDependentEntryDefinition.AppIds { get; set; } = [];

    internal override void FillClone(IRunEntryDefinition definition)
    {
        base.FillClone(definition);
        ((IDependentEntryDefinition)definition).AppIds = new List<string>(((IDependentEntryDefinition)this).AppIds);
    }
}
