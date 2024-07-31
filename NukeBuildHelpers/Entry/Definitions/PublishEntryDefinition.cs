using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;

namespace NukeBuildHelpers.Entry.Definitions;

internal class PublishEntryDefinition : TargetEntryDefinition, IPublishEntryDefinition
{
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
}
