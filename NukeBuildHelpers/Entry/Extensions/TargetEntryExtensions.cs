using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NukeBuildHelpers.Entry.Interfaces;

namespace NukeBuildHelpers.Entry.Extensions;

public static class TargetEntryExtensions
{
    public static TTargetEntryDefinition AppId<TTargetEntryDefinition>(this TTargetEntryDefinition definition, string appId)
        where TTargetEntryDefinition : ITargetEntryDefinition
    {
        definition.AppId = appId;
        return definition;
    }
}
