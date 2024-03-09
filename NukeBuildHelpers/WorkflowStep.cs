using NukeBuildHelpers.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers;

public abstract class WorkflowStep : BaseHelper
{
    public virtual int Priority { get; set; } = 0;

    public virtual void TestRun(AppTestEntry appTestEntry) { }

    public virtual void AppBuild(AppEntry appEntry) { }

    public virtual void AppPublish(AppEntry appEntry) { }
}

public abstract class WorkflowStep<TBuild> : WorkflowStep
    where TBuild : BaseNukeBuildHelpers
{
    public new TBuild NukeBuild => (TBuild)base.NukeBuild;
}
