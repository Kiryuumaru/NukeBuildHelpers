using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Core.Tokens;

namespace NukeBuildHelpers.ConsoleInterface.Models;

internal class ConsoleTableCell
{
    internal static ConsoleTableCell Empty { get; } = FromValue(null);

    internal static ConsoleTableCell FromValue(string? value, ConsoleColor color = ConsoleColor.Magenta)
    {
        return new ConsoleTableCell()
        {
            Value = value,
            Color = color,
        };
    }

    public required string? Value { get; init; }

    public required ConsoleColor Color { get; init; }

    public static implicit operator ConsoleTableCell((string? value, ConsoleColor color) valuePair)
    {
        return FromValue(valuePair.value, valuePair.color);
    }

    public static implicit operator ConsoleTableCell(string? value)
    {
        return FromValue(value);
    }
}
