using NukeBuildHelpers.Entry.Interfaces;

namespace NukeBuildHelpers.Entry.Models;

internal class AppEntry
{
    public required string AppId { get; init; }

    public List<ITestEntryDefinition> TestEntryDefinitions { get; } = [];

    public List<IBuildEntryDefinition> BuildEntryDefinitions { get; } = [];

    public List<IPublishEntryDefinition> PublishEntryDefinitions { get; } = [];

    public List<IRunEntryDefinition> RunEntryDefinitions { get; } = [];
}
