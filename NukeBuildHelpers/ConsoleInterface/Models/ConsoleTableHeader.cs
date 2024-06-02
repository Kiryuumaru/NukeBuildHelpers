using NukeBuildHelpers.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.ConsoleInterface.Models;

internal class ConsoleTableHeader
{
    internal static ConsoleTableHeader FromValue(string value, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        return new()
        {
            Value = value,
            Alignment = alignment
        };
    }

    public required string Value { get; init; }

    public required HorizontalAlignment Alignment { get; init; }

    public static implicit operator ConsoleTableHeader((string value, HorizontalAlignment alignment) valuePair)
    {
        return FromValue(valuePair.value, valuePair.alignment);
    }
}
