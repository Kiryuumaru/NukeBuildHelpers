using Microsoft.Build.Tasks;
using Nuke.Common;
using Nuke.Common.IO;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace NukeBuildHelpers;

public abstract class BaseEntry
{
    public virtual bool Enable { get; } = true;

    public virtual string Id
    {
        get
        {
            return GetType().Name.ToSnakeCase();
        }
    }

    public virtual string Name
    {
        get
        {
            return GetType().Name;
        }
    }

    public abstract RunsOnType RunsOn { get; }
}
