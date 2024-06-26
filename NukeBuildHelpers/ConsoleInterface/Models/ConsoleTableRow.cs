namespace NukeBuildHelpers.ConsoleInterface.Models;

internal class ConsoleTableRow
{
    internal static ConsoleTableRow Separator { get; } = new ConsoleTableRow()
    {
        IsSeperator = true,
        Cells = []
    };

    internal static ConsoleTableRow FromValue(params ConsoleTableCell[] cells)
    {
        return new()
        {
            IsSeperator = false,
            Cells = cells
        };
    }

    public bool IsSeperator { get; init; }

    public required ConsoleTableCell[] Cells { get; init; }

    public static implicit operator ConsoleTableRow(ConsoleTableCell[] cells)
    {
        return FromValue(cells);
    }
}
