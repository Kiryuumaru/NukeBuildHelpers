using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using NukeBuildHelpers.Models;
using Serilog;

namespace NukeBuildHelpers;

public class BaseNukeBuildHelpers : NukeBuild, INukeBuildHelpers
{
    public IReadOnlyList<AppConfig<AppEntryConfig>> AppEntryConfigs { get; private set; }

    public IReadOnlyList<AppConfig<AppTestConfig>> AppTestConfigs { get; private set; }

    protected override void OnBuildInitialized()
    {
        base.OnBuildInitialized();

        AppEntryConfigs = (this as INukeBuildHelpers).GetAppEntries<AppEntryConfig>().AsReadOnly();
        AppTestConfigs = (this as INukeBuildHelpers).GetAppTests<AppTestConfig>().AsReadOnly();
    }
}
