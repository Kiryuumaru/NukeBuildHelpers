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
    public static AbsolutePath RootDirectory => NukeBuild.RootDirectory;

    public static AbsolutePath TemporaryDirectory => NukeBuild.TemporaryDirectory;

    public static AbsolutePath OutputDirectory => BaseNukeBuildHelpers.OutputDirectory;

    public virtual bool Enable { get; } = true;

    public virtual bool RunParallel { get; } = true;

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
}
