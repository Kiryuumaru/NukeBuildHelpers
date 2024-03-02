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

    public NewVersion NewVersion => InternalNewVersion;

    internal NewVersion InternalNewVersion = null;

    internal Action<BaseNukeBuildHelpers, AbsolutePath> PrepareImpl;
    internal Action<BaseNukeBuildHelpers, AbsolutePath> BuildImpl;
    internal Action<BaseNukeBuildHelpers, AbsolutePath> PackImpl;
    internal Action<BaseNukeBuildHelpers, AbsolutePath> ReleaseImpl;

    internal void PrepareCore(BaseNukeBuildHelpers nukeBuild, AbsolutePath outputPath) => PrepareImpl(nukeBuild, outputPath);

    internal void BuildCore(BaseNukeBuildHelpers nukeBuild, AbsolutePath outputPath) => BuildImpl(nukeBuild, outputPath);

    internal void PackCore(BaseNukeBuildHelpers nukeBuild, AbsolutePath outputPath) => PackImpl(nukeBuild, outputPath);

    internal void ReleaseCore(BaseNukeBuildHelpers nukeBuild, AbsolutePath outputPath) => ReleaseImpl(nukeBuild, outputPath);
}

public abstract class AppEntry<TBuild> : AppEntry
    where TBuild : BaseNukeBuildHelpers
{
    protected AppEntry()
    {
        PrepareImpl = (build, path) => Prepare(build as TBuild, path);
        BuildImpl = (build, path) => Build(build as TBuild, path);
        PackImpl = (build, path) => Pack(build as TBuild, path);
        ReleaseImpl = (build, path) => Publish(build as TBuild, path);
    }

    public virtual void Prepare(TBuild nukeBuild, AbsolutePath outputPath) { }

    public virtual void Build(TBuild nukeBuild, AbsolutePath outputPath) { }

    public virtual void Pack(TBuild nukeBuild, AbsolutePath outputPath) { }

    public virtual void Publish(TBuild nukeBuild, AbsolutePath outputPath) { }
}
