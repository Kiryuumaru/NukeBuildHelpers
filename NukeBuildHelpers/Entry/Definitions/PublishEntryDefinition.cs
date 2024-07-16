using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;

namespace NukeBuildHelpers.Entry.Definitions;

internal class PublishEntryDefinition : TargetEntryDefinition, IPublishEntryDefinition
{
    protected override string GetDefaultName()
    {
        return "Publish - " + ((IPublishEntryDefinition)this).AppId + " (" + Id + ")";
    }

    protected override IRunEntryDefinition Clone()
    {
        var definition = new PublishEntryDefinition()
        {
            Id = Id
        };
        FillClone(definition);
        return definition;
    }
}
