namespace NukeBuildHelpers.Entry.Interfaces;

public interface IDependentEntryDefinition : IEntryDefinition
{
    internal string[] AppIds { get; set; }
}
