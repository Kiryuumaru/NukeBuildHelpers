using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using NukeBuildHelpers.Models;
using Octokit;
using Semver;
using Serilog;
using System.Text.Json;

namespace NukeBuildHelpers;

public partial class BaseNukeBuildHelpers : NukeBuild, INukeBuildHelpers
{
    public AbsolutePath PublishPath { get; } = RootDirectory / "publish";

    public AbsolutePath ArtifactsPath { get; } = RootDirectory / "publish" / "artifacts";

    public IReadOnlyList<AppConfig<AppEntryConfig>> AppEntryConfigs { get; private set; }

    public IReadOnlyList<AppConfig<AppTestConfig>> AppTestConfigs { get; private set; }

    public IReadOnlyDictionary<string, string> SplitTargetArgs { get; private set; }

    GitRepository Repository => (this as INukeBuildHelpers).Repository;

    string TargetParams => (this as INukeBuildHelpers).TargetArgs;

    Tool Git => (this as INukeBuildHelpers).Git;

    static readonly JsonSerializerOptions jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    protected override void OnBuildInitialized()
    {
        base.OnBuildInitialized();

        AppEntryConfigs = GetAppEntries<AppEntryConfig>().AsReadOnly();
        AppTestConfigs = GetAppTests<AppTestConfig>().AsReadOnly();
        SplitTargetArgs = GetTargetParams().AsReadOnly();
    }
}
