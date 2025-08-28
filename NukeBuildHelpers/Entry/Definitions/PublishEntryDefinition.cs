using Nuke.Common.IO;
using NukeBuildHelpers.Entry.Extensions;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;
using Octokit;

namespace NukeBuildHelpers.Entry.Definitions;

internal class PublishEntryDefinition : TargetEntryDefinition, IPublishEntryDefinition
{
    protected override string GetDefaultName()
    {
        if (((IPublishEntryDefinition)this).AppIds.Count > 1 || ((IPublishEntryDefinition)this).AppIds.Count == 0)
        {
            return "Publish - " + ((IPublishEntryDefinition)this).Id;
        }
        else
        {
            return "Publish - " + ((IPublishEntryDefinition)this).AppIds.First() + " (" + ((IPublishEntryDefinition)this).Id + ")";
        }
    }

    protected override IRunEntryDefinition Clone()
    {
        var definition = new PublishEntryDefinition();
        FillClone(definition);
        return definition;
    }
}
