using NukeBuildHelpers.Entry.Interfaces;

namespace NukeBuildHelpers.Entry.Extensions;

public static class DependentEntryExtensions
{
    public static TDependentEntryDefinition AppId<TDependentEntryDefinition>(this TDependentEntryDefinition definition, params string[] appIds)
        where TDependentEntryDefinition : ITestEntryDefinition
    {
        definition.AppIds = appIds;
        return definition;
    }
}
