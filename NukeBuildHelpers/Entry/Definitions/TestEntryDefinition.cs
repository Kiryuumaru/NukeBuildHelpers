using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;

namespace NukeBuildHelpers.Entry.Definitions;

internal class TestEntryDefinition : DependentEntryDefinition, ITestEntryDefinition
{
    protected override string GetDefaultName()
    {
        if (((ITestEntryDefinition)this).AppIds.Count > 1 || ((ITestEntryDefinition)this).AppIds.Count == 0)
        {
            return "Test - " + ((ITestEntryDefinition)this).Id;
        }
        else
        {
            return "Test - " + ((ITestEntryDefinition)this).AppIds.First() + " (" + ((ITestEntryDefinition)this).Id + ")";
        }
    }

    protected override IRunEntryDefinition Clone()
    {
        var definition = new TestEntryDefinition();
        FillClone(definition);
        return definition;
    }
}
