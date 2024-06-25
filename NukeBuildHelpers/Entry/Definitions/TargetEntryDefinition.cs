using NukeBuildHelpers.Entry.Interfaces;

namespace NukeBuildHelpers.Entry.Definitions;

internal abstract class TargetEntryDefinition : EntryDefinition, IPublishEntryDefinition
{
    string? ITargetEntryDefinition.AppId { get; set; }
}
