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

public abstract class AppEntry : BaseEntry
{
    public virtual bool MainRelease { get; } = true;

    public AbsolutePath OutputPath { get; internal set; } = null!;

    public BaseNukeBuildHelpers NukeBuild { get; internal set; } = null!;

    public NewVersion? NewVersion { get; internal set; }

    public virtual void Prepare() { }

    public virtual void Build() { }

    public virtual void Publish() { }
}

public abstract class AppEntry<TBuild> : AppEntry
    where TBuild : BaseNukeBuildHelpers
{
    public new TBuild NukeBuild => (TBuild)base.NukeBuild;
}
