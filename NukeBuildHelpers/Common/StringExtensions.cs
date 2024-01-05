using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Common;

internal static class StringExtensions
{
    public static string PadLeft(this string str, int totalWidth, int length) => PadLeft(str, totalWidth, length, ' ');

    public static string PadLeft(this string str, int totalWidth, int length, char paddingChar)
    {
        if (totalWidth < 0)
            throw new ArgumentOutOfRangeException(nameof(totalWidth));
        int oldLength = length;
        int count = totalWidth - oldLength;
        if (count <= 0)
            return str;

        return new string(paddingChar, count) + str;
    }

    public static string PadRight(this string str, int totalWidth, int length) => PadRight(str, totalWidth, length, ' ');

    public static string PadRight(this string str, int totalWidth, int length, char paddingChar)
    {
        if (totalWidth < 0)
            throw new ArgumentOutOfRangeException(nameof(totalWidth));
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
        if (totalWidth < 0)
            throw new ArgumentOutOfRangeException(nameof(totalWidth));
        int oldLength = length;
        int count = totalWidth - oldLength;
        if (count <= 0)
            return str;

        int padLeft = totalWidth / 2 - length / 2;
        int padRight = totalWidth - padLeft - length;

        return new string(paddingChar, padLeft) + str + new string(paddingChar, padRight);
    }

}
