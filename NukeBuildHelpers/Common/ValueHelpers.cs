using Nuke.Common;
using NukeBuildHelpers.Entry.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Common;

internal static class ValueHelpers
{
    internal static T GetOrNullFail<T>([NotNull] T? val, [CallerArgumentExpression(nameof(val))] string? paramName = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(val, paramName);
        return val;
    }

    internal static void GetOrFail<T>(Func<T> valFactory, out T valOut)
    {
        try
        {
            valOut = valFactory();
        }
        catch (Exception ex)
        {
            Assert.Fail(ex.Message, ex);
            throw;
        }
    }

    internal static void GetOrFail(string appId, AllEntry allEntries, out AppEntry appEntry)
    {
        try
        {
            if (string.IsNullOrEmpty(appId))
            {
                throw new InvalidOperationException($"AppId should not be empty");
            }

            if (!allEntries.AppEntryMap.TryGetValue(appId, out AppEntry? appEntryFromMap))
            {
                throw new InvalidOperationException($"App id \"{appId}\" does not exists");
            }

            appEntry = appEntryFromMap;
        }
        catch (Exception ex)
        {
            Assert.Fail(ex.Message, ex);
            throw;
        }
    }
}
