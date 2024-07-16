using NukeBuildHelpers.Entry.Interfaces;

namespace NukeBuildHelpers.Entry.Models;

internal class AllEntry
{
    public required Dictionary<string, AppEntry> AppEntryMap { get; init; }

    public required IWorkflowConfigEntryDefinition WorkflowConfigEntryDefinition { get; init; }

    public required Dictionary<string, ITestEntryDefinition> TestEntryDefinitionMap { get; init; }

    public required Dictionary<string, IBuildEntryDefinition> BuildEntryDefinitionMap { get; init; }

    public required Dictionary<string, IPublishEntryDefinition> PublishEntryDefinitionMap { get; init; }

    public required Dictionary<string, IRunEntryDefinition> RunEntryDefinitionMap { get; init; }

    public required Dictionary<string, ITargetEntryDefinition> TargetEntryDefinitionMap { get; init; }

    public required Dictionary<string, IDependentEntryDefinition> DependentEntryDefinitionMap { get; init; }
}
