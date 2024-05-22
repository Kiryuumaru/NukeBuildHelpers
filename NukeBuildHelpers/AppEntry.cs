using JetBrains.Annotations;
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

public abstract class AppEntry : Entry
{
    public abstract RunsOnType BuildRunsOn { get; }

    public abstract RunsOnType PublishRunsOn { get; }

    public virtual RunType RunType { get; } = RunType.Local | RunType.PullRequest | RunType.Bump | RunType.Commit;

    public virtual bool MainRelease { get; } = true;

    public virtual void Build(AppRunContext runContext) { }

    public virtual void Publish(AppRunContext runContext) { }

    internal AppRunContext? AppRunContext { get; set; }
}

public abstract class AppEntry<TBuild> : AppEntry
    where TBuild : BaseNukeBuildHelpers
{
    public new TBuild NukeBuild => (TBuild)base.NukeBuild;
}
