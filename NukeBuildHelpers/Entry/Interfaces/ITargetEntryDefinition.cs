namespace NukeBuildHelpers.Entry.Interfaces;

public interface ITargetEntryDefinition : IEntryDefinition
{
    internal string? AppId { get; set; }
}
