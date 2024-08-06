using NukeBuildHelpers.Entry.Interfaces;

namespace NukeBuildHelpers.Entry.Definitions;

internal abstract class DependentEntryDefinition : RunEntryDefinition, IDependentEntryDefinition
{
    List<string>? appIds;
    List<string> IDependentEntryDefinition.AppIds
    {
        get => appIds ?? [];
        set => appIds = value;
    }

    internal override void FillClone(IRunEntryDefinition definition)
    {
        base.FillClone(definition);
        if (appIds != null) ((IDependentEntryDefinition)definition).AppIds = new List<string>(appIds);
    }
}
