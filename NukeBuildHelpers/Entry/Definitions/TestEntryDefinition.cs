using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;

namespace NukeBuildHelpers.Entry.Definitions;

internal class TestEntryDefinition : DependentEntryDefinition, ITestEntryDefinition
{
    protected override string GetDefaultName()
    {
        if (((ITestEntryDefinition)this).AppIds.Length > 1 || ((ITestEntryDefinition)this).AppIds.Length == 0)
        {
            return "Test - " + ((ITestEntryDefinition)this).Id;
        }
        else
        {
            return "Test - " + ((ITestEntryDefinition)this).AppIds.First();
        }
    }

    protected override Task<bool> GetDefaultCondition(IRunContext runContext)
    {
        return Task.FromResult(true);
    }
}
