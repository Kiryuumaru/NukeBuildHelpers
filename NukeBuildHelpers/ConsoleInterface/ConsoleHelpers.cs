namespace NukeBuildHelpers.ConsoleInterface;

internal static class ConsoleHelpers
{
    public static void ClearCurrentConsoleLine()
    {
        int currentLineCursor = Console.CursorTop;
        Console.SetCursorPosition(0, Console.CursorTop);
        Console.Write(new string(' ', Console.WindowWidth));
        Console.SetCursorPosition(0, currentLineCursor);
    }

    public static void WriteLineClean(string text)
    {
        ClearCurrentConsoleLine();
        Console.WriteLine(text);
    }

    public static void WriteWithColor(string text, ConsoleColor textColor)
    {
        var consoleColor = Console.ForegroundColor;
        Console.ForegroundColor = textColor;
        Console.Write(text);
        Console.ForegroundColor = consoleColor;
    }
}
