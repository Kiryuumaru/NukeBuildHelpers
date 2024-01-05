using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Common;

internal static class StringExtensions
{
    public static string PadCenter(this string str, int length)
    {
        int spaces = length - str.Length;
        int padLeft = spaces / 2 + str.Length;
        return str.PadLeft(padLeft).PadRight(length);
    }
}
