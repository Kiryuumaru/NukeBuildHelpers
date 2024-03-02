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

namespace NukeBuildHelpers.Models;

public abstract class AppTestEntry : BaseEntry
{
    public abstract Type[] AppEntryTargets { get; }

    internal Action<BaseNukeBuildHelpers>? PrepareImpl;
    internal Action<BaseNukeBuildHelpers>? RunImpl;

    internal void PrepareCore(BaseNukeBuildHelpers nukeBuild) => PrepareImpl?.Invoke(nukeBuild);

    internal void RunCore(BaseNukeBuildHelpers nukeBuild) => RunImpl?.Invoke(nukeBuild);
}

public abstract class AppTestEntry<TBuild> : AppTestEntry
    where TBuild : BaseNukeBuildHelpers
{
    protected AppTestEntry()
    {
        PrepareImpl = (build) => Prepare((TBuild)build);
        RunImpl = (build) => Run((TBuild)build);
    }

    public virtual void Prepare(TBuild nukeBuild) { }

    public virtual void Run(TBuild nukeBuild) { }
}
