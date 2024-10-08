﻿using Nuke.Common;
using NukeBuildHelpers.Entry.Models;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

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

    internal static async Task<T> GetOrFail<T>(Func<Task<T>> valFactory)
    {
        try
        {
            return await valFactory();
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
