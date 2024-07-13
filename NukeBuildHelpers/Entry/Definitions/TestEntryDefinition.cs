using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;

namespace NukeBuildHelpers.Entry.Definitions;

internal class TestEntryDefinition : DependentEntryDefinition, ITestEntryDefinition
{
    protected override string GetDefaultName()
    {
        if (((ITestEntryDefinition)this).AppIds.Length > 1 || ((ITestEntryDefinition)this).AppIds.Length == 0)
        {
            return "Test - " + ((ITestEntryDefinition)this).Id + " (" + Id + ")";
        }
        else
        {
            return "Test - " + ((ITestEntryDefinition)this).AppIds.First() + " (" + Id + ")";
        }
    }

    protected override IEntryDefinition Clone()
    {
        var definition = new TestEntryDefinition()
        {
            Id = Id
        };
        FillClone(definition);
        return definition;
    }
}
