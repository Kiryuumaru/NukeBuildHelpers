using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;

namespace NukeBuildHelpers.Entry.Definitions;

internal class BuildEntryDefinition : TargetEntryDefinition, IBuildEntryDefinition
{
    protected override string GetDefaultName()
    {
        return "Build - " + ((IBuildEntryDefinition)this).AppId;
    }

    protected override Task<bool> GetDefaultCondition(IRunContext runContext)
    {
        return Task.FromResult(true);
    }
}
