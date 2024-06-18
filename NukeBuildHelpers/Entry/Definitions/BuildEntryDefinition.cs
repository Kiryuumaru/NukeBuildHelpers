using NukeBuildHelpers.Entry.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Entry.Definitions;

internal class BuildEntryDefinition : TargetEntryDefinition, IBuildEntryDefinition
{
    protected override string GetDefaultName()
    {
        return "Build - " + ((IBuildEntryDefinition)this).AppId;
    }
}
