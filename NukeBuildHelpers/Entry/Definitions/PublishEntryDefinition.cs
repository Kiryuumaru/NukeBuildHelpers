﻿using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.RunContext.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Entry.Definitions;

internal class PublishEntryDefinition : TargetEntryDefinition, IPublishEntryDefinition
{
    protected override string GetDefaultName()
    {
        return "Publish - " + ((IPublishEntryDefinition)this).AppId;
    }

    protected override Task<bool> GetDefaultCondition(IRunContext runContext)
    {
        return Task.FromResult(runContext.RunType == Common.Enums.RunType.Bump);
    }
}