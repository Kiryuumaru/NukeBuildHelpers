using System.Text;

namespace NukeBuildHelpers.Common;

internal static class StringExtensions
{
    public static string PadLeft(this string str, int totalWidth, int length) => PadLeft(str, totalWidth, length, ' ');

    public static string PadLeft(this string str, int totalWidth, int length, char paddingChar)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalWidth);

        int oldLength = length;
        int count = totalWidth - oldLength;
        if (count <= 0)
            return str;

        return new string(paddingChar, count) + str;
    }

    public static string PadRight(this string str, int totalWidth, int length) => PadRight(str, totalWidth, length, ' ');

    public static string PadRight(this string str, int totalWidth, int length, char paddingChar)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalWidth);

        int oldLength = length;
        int count = totalWidth - oldLength;
        if (count <= 0)
            return str;

        return str + new string(paddingChar, count);
    }

    public static string PadCenter(this string str, int totalWidth) => PadCenter(str, totalWidth, ' ');

    public static string PadCenter(this string str, int totalWidth, char paddingChar)
    {
        int spaces = totalWidth - str.Length;
        int padLeft = spaces / 2 + str.Length;
        return str.PadLeft(padLeft, paddingChar).PadRight(totalWidth, paddingChar);
    }

    public static string PadCenter(this string str, int totalWidth, int length) => PadCenter(str, totalWidth, length, ' ');

    public static string PadCenter(this string str, int totalWidth, int length, char paddingChar)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalWidth);

        int oldLength = length;
        int count = totalWidth - oldLength;
        if (count <= 0)
            return str;

        int padLeft = totalWidth / 2 - length / 2;
        int padRight = totalWidth - padLeft - length;

        return new string(paddingChar, padLeft) + str + new string(paddingChar, padRight);
    }

    public static string ToSnakeCase(this string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length < 2)
        {
            return text;
        }
        var sb = new StringBuilder();
        sb.Append(char.ToLowerInvariant(text[0]));
        for (int i = 1; i < text.Length; ++i)
        {
            char c = text[i];
            if (char.IsUpper(c))
            {
                sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    public static string StringToBase64(this string plainText)
    {
        byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
        return Convert.ToBase64String(plainTextBytes);
    }

    public static string Base64ToString(this string base64Encoded)
    {
        byte[] base64Bytes = Convert.FromBase64String(base64Encoded);
        return Encoding.UTF8.GetString(base64Bytes);
    }
}
