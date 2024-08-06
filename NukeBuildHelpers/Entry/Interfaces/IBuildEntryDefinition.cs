using Nuke.Common.IO;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.RunContext.Interfaces;

namespace NukeBuildHelpers.Entry.Interfaces;

/// <summary>
/// Interface defining a build-related entry in the build system.
/// </summary>
public interface IBuildEntryDefinition : ITargetEntryDefinition
{
    internal List<Func<IRunContext, Task<AbsolutePath[]>>> ReleaseAsset { get; set; }

    internal List<Func<IRunContext, Task<AbsolutePath[]>>> CommonReleaseAsset { get; set; }

    internal Task<AbsolutePath[]> GetReleaseAssets() => GetAssets(ReleaseAsset);

    internal Task<AbsolutePath[]> GetCommonReleaseAssets() => GetAssets(CommonReleaseAsset);

    private async Task<AbsolutePath[]> GetAssets(List<Func<IRunContext, Task<AbsolutePath[]>>> assetFactories)
    {
        List<AbsolutePath> assets = [];
        foreach (var releaseAsset in assetFactories)
        {
            assets.AddRange(await releaseAsset(ValueHelpers.GetOrNullFail(RunContext)));
        }
        return [.. assets];
    }
}
