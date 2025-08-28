using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Common.Models;
using NukeBuildHelpers.Entry.Helpers;
using Serilog;

namespace NukeBuildHelpers;

partial class BaseNukeBuildHelpers
{
    internal List<AbsolutePath> ReleaseAssets { get; } = [];

    internal List<AbsolutePath> ReleaseCommonAssets { get; } = [];

    /// <summary>
    /// Adds a file or directory path to the collection of individual release assets.
    /// If the path is a directory, it will be zipped before being uploaded to the release.
    /// </summary>
    /// <param name="path">The absolute path to the file or directory to include as a release asset.</param>
    public void AddReleaseAsset(AbsolutePath path)
    {
        ReleaseAssets.Add(path);
    }

    /// <summary>
    /// Adds a file or directory path to the collection of common release assets.
    /// These assets will be bundled together with other common assets from the same app ID into a single zip archive.
    /// </summary>
    /// <param name="path">The absolute path to the file or directory to include as a common release asset.</param>
    public void AddReleaseCommonAsset(AbsolutePath path)
    {
        ReleaseCommonAssets.Add(path);
    }
}
