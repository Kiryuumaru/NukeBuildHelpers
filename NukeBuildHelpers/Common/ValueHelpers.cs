using Nuke.Common;
using NukeBuildHelpers.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Common;

internal static class ValueHelpers
{
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

    internal static void GetOrFail(string appId, Dictionary<string, AppEntryConfig> appEntryConfigs, out string appIdOut, out AppEntryConfig appEntryConfig)
    {
        try
        {
            if (string.IsNullOrEmpty(appId) && !appEntryConfigs.Any(ae => ae.Value.Entry.MainRelease))
            {
                throw new InvalidOperationException($"App entries has no main release, appId should not be empty");
            }

            if (!string.IsNullOrEmpty(appId))
            {
                if (!appEntryConfigs.TryGetValue(appId.ToLowerInvariant(), out var aec) || aec == null)
                {
                    throw new InvalidOperationException($"App id \"{appId}\" does not exists");
                }
                appEntryConfig = aec;
            }
            else
            {
                appEntryConfig = appEntryConfigs.Where(ae => ae.Value.Entry.MainRelease).First().Value;
            }

            appIdOut = appEntryConfig.Entry.Id;
        }
        catch (Exception ex)
        {
            Assert.Fail(ex.Message, ex);
            throw;
        }
    }
}
