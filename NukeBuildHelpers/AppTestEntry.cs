using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Nuke.Common;
using Nuke.Common.IO;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Enums;

namespace NukeBuildHelpers;

public abstract class AppTestEntry : Entry
{
    public abstract RunsOnType RunsOn { get; }

    public virtual TestRunType RunType { get; } = TestRunType.Always;

    public abstract Type[] AppEntryTargets { get; }

    public virtual void Run() { }
}

public abstract class AppTestEntry<TBuild> : AppTestEntry
    where TBuild : BaseNukeBuildHelpers
{
    public new TBuild NukeBuild => (TBuild)base.NukeBuild;
}
