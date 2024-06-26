using NukeBuildHelpers.Entry.Interfaces;

namespace NukeBuildHelpers.Entry.Definitions;

internal abstract class DependentEntryDefinition : EntryDefinition, IDependentEntryDefinition
{
    string[] IDependentEntryDefinition.AppIds { get; set; } = [];
}
