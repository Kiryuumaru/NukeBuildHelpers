using NukeBuildHelpers.Entry.Interfaces;

namespace NukeBuildHelpers.Entry.Definitions;

internal class BuildEntryDefinition : TargetEntryDefinition, IBuildEntryDefinition
{
    protected override string GetDefaultName()
    {
        if (((IBuildEntryDefinition)this).AppIds.Count > 1 || ((IBuildEntryDefinition)this).AppIds.Count == 0)
        {
            return "Build - " + ((IBuildEntryDefinition)this).Id;
        }
        else
        {
            return "Build - " + ((IBuildEntryDefinition)this).AppIds.First() + " (" + ((IBuildEntryDefinition)this).Id + ")";
        }
    }

    protected override IRunEntryDefinition Clone()
    {
        var definition = new BuildEntryDefinition();
        FillClone(definition);
        return definition;
    }
}
