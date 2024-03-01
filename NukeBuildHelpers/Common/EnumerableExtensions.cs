using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Common;

internal static class EnumerableExtensions
{
    internal static IEnumerable<T> Combine<T>(this IEnumerable<T> source, IEnumerable<T> toCombine)
    {
        List<T> result = new();
        foreach (var item in source)
        {
            result.Add(item);
        }
        foreach (var item in toCombine)
        {
            result.Add(item);
        }
        return result;
    }
}
