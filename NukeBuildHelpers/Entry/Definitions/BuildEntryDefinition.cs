using NukeBuildHelpers.Entry.Interfaces;

namespace NukeBuildHelpers.Entry.Definitions;

internal class BuildEntryDefinition : TargetEntryDefinition, IBuildEntryDefinition
{
    protected override string GetDefaultName()
    {
        return "Build - " + ((IBuildEntryDefinition)this).AppId + " (" + ((IBuildEntryDefinition)this).Id + ")";
    }

    protected override IRunEntryDefinition Clone()
    {
        var definition = new BuildEntryDefinition();
        FillClone(definition);
        return definition;
    }
}
