using NukeBuildHelpers.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers;

public abstract class WorkflowBuilder : BaseHelper
{
    public virtual int Priority { get; set; } = 0;

    public virtual void WorkflowBuilderPreTestRun(Dictionary<string, object> step) { }

    public virtual void WorkflowBuilderPostTestRun(Dictionary<string, object> step) { }

    public virtual void WorkflowBuilderPreBuildRun(Dictionary<string, object> step) { }

    public virtual void WorkflowBuilderPostBuildRun(Dictionary<string, object> step) { }

    public virtual void WorkflowBuilderPrePublishRun(Dictionary<string, object> step) { }

    public virtual void WorkflowBuilderPostPublishRun(Dictionary<string, object> step) { }
}

public abstract class WorkflowBuilder<TBuild> : WorkflowBuilder
    where TBuild : BaseNukeBuildHelpers
{
    public new TBuild NukeBuild => (TBuild)base.NukeBuild;
}
