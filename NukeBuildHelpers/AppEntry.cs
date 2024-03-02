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

    public NewVersion? NewVersion => InternalNewVersion;

    internal NewVersion? InternalNewVersion = null;

    internal Action<BaseNukeBuildHelpers, AbsolutePath>? PrepareImpl;
    internal Action<BaseNukeBuildHelpers, AbsolutePath>? BuildImpl;
    internal Action<BaseNukeBuildHelpers, AbsolutePath>? PublishImpl;

    internal void PrepareCore(BaseNukeBuildHelpers nukeBuild, AbsolutePath outputPath) => PrepareImpl?.Invoke(nukeBuild, outputPath);

    internal void BuildCore(BaseNukeBuildHelpers nukeBuild, AbsolutePath outputPath) => BuildImpl?.Invoke(nukeBuild, outputPath);

    internal void PublishCore(BaseNukeBuildHelpers nukeBuild, AbsolutePath outputPath) => PublishImpl?.Invoke(nukeBuild, outputPath);
}

public abstract class AppEntry<TBuild> : AppEntry
    where TBuild : BaseNukeBuildHelpers
{
    protected AppEntry()
    {
        PrepareImpl = (build, path) => Prepare((TBuild)build, path);
        BuildImpl = (build, path) => Build((TBuild)build, path);
        PublishImpl = (build, path) => Publish((TBuild)build, path);
    }

    public virtual void Prepare(TBuild nukeBuild, AbsolutePath outputPath) { }

    public virtual void Build(TBuild nukeBuild, AbsolutePath outputPath) { }

    public virtual void Publish(TBuild nukeBuild, AbsolutePath outputPath) { }
}
