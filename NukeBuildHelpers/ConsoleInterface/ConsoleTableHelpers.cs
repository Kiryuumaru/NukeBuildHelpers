using NukeBuildHelpers.Common;
using NukeBuildHelpers.ConsoleInterface.Enums;
using NukeBuildHelpers.ConsoleInterface.Models;

namespace NukeBuildHelpers.ConsoleInterface;

internal static class ConsoleTableHelpers
{
    internal static void LogInfoTable(ConsoleTableHeader[] headers, ConsoleTableRow[] rows)
    {
        List<(int Length, string Text, HorizontalAlignment Alignment)> columns = [];

        foreach (var header in headers)
        {
            columns.Add((header.Value.Length, header.Value, header.Alignment));
        }

        foreach (var row in rows)
        {
            int rowCount = row.Cells.Length;
            for (int i = 0; i < rowCount; i++)
            {
                int cellWidth = row.Cells[i].Value?.Length ?? 0;
                columns[i] = (MathExtensions.Max(rowCount, columns[i].Length, cellWidth), columns[i].Text, columns[i].Alignment);
            }
        }

        string headerSeparator = "╬";
        string rowSeparator = "║";
        string textHeader = "║";
        foreach (var (Length, Text, AlignRight) in columns)
        {
            headerSeparator += new string('═', Length + 2) + '╬';
            rowSeparator += new string('-', Length + 2) + '║';
            textHeader += Text.PadCenter(Length + 2) + '║';
        }

        Console.WriteLine(headerSeparator);
        Console.WriteLine(textHeader);
        Console.WriteLine(headerSeparator);
        foreach (var row in rows)
        {
            if (row.IsSeperator)
            {
                Console.WriteLine(rowSeparator);
            }
            else
            {
                var cells = row.Cells.Select(i => i.Value?.ToString() ?? "null")?.ToArray() ?? [];
                int rowCount = row.Cells.Length;
                Console.Write("║ ");
                for (int i = 0; i < rowCount; i++)
                {
                    string rowText = cells[i];
                    var textRow = columns[i].Alignment switch
                    {
                        HorizontalAlignment.Left => rowText.PadLeft(columns[i].Length, rowText.Length),
                        HorizontalAlignment.Center => rowText.PadCenter(columns[i].Length, rowText.Length),
                        HorizontalAlignment.Right => rowText.PadRight(columns[i].Length, rowText.Length),
                        _ => throw new NotImplementedException()
                    };
                    ConsoleHelpers.WriteWithColor(textRow, ConsoleColor.Magenta);
                    Console.Write(" ║ ");
                }
                Console.WriteLine();
            }
        }
        Console.WriteLine(headerSeparator);
    }

    internal static int LogInfoTableWatch(ConsoleTableHeader[] headers, ConsoleTableRow[] rows)
    {
        int lines = 0;

        List<(int Length, string Text, HorizontalAlignment Alignment)> columns = [];

        foreach (var header in headers)
        {
            columns.Add((header.Value.Length, header.Value, header.Alignment));
        }

        foreach (var row in rows)
        {
            int rowCount = row.Cells.Length;
            for (int i = 0; i < rowCount; i++)
            {
                int rowWidth = row.Cells[i].Value?.Length ?? 0;
                columns[i] = (MathExtensions.Max(rowCount, columns[i].Length, rowWidth), columns[i].Text, columns[i].Alignment);
            }
        }

        string headerSeparator = "╬";
        string rowSeparator = "║";
        string textHeader = "║";
        foreach (var (Length, Text, AlignRight) in columns)
        {
            headerSeparator += new string('═', Length + 2) + '╬';
            rowSeparator += new string('-', Length + 2) + '║';
            textHeader += Text.PadCenter(Length + 2) + '║';
        }

        ConsoleHelpers.WriteLineClean(headerSeparator);
        ConsoleHelpers.WriteLineClean(textHeader);
        ConsoleHelpers.WriteLineClean(headerSeparator);
        lines++;
        lines++;
        lines++;
        foreach (var row in rows)
        {
            if (row.IsSeperator)
            {
                ConsoleHelpers.WriteLineClean(rowSeparator);
                lines++;
            }
            else
            {
                ConsoleHelpers.ClearCurrentConsoleLine();
                var cells = row.Cells.Select(i => i.Value?.ToString() ?? "null")?.ToArray() ?? [];
                Console.Write("║ ");
                for (int i = 0; i < row.Cells.Length; i++)
                {
                    var rowText = row.Cells[i].Value;
                    var rowTextColor = row.Cells[i].Color;
                    int rowWidth = rowText == null ? 4 : rowText.Length;
                    var cellText = columns[i].Alignment switch
                    {
                        HorizontalAlignment.Left => cells[i].PadLeft(columns[i].Length, rowWidth),
                        HorizontalAlignment.Center => cells[i].PadCenter(columns[i].Length, rowWidth),
                        HorizontalAlignment.Right => cells[i].PadRight(columns[i].Length, rowWidth),
                        _ => throw new NotImplementedException()
                    };
                    ConsoleHelpers.WriteWithColor(cellText, rowTextColor);
                    Console.Write(" ║ ");
                }
                Console.WriteLine();
                lines++;
            }
        }
        ConsoleHelpers.WriteLineClean(headerSeparator);
        lines++;

        return lines;
    }
}
