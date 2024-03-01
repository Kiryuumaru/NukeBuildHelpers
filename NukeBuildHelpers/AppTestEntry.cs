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

    internal Action<BaseNukeBuildHelpers> PrepareImpl;
    internal Action<BaseNukeBuildHelpers> RunImpl;

    internal void PrepareCore(BaseNukeBuildHelpers nukeBuild) => PrepareImpl(nukeBuild);

    internal void RunCore(BaseNukeBuildHelpers nukeBuild) => RunImpl(nukeBuild);
}

public abstract class AppTestEntry<TBuild> : AppTestEntry
    where TBuild : BaseNukeBuildHelpers
{
    protected AppTestEntry()
    {
        PrepareImpl = (build) => Prepare(build as TBuild);
        RunImpl = (build) => Run(build as TBuild);
    }

    public virtual void Prepare(TBuild nukeBuild) { }

    public virtual void Run(TBuild nukeBuild) { }
}
