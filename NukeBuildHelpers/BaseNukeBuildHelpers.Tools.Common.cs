using NuGet.ContentModel;
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
    /// <summary>
    /// Adds a file or directory path to the collection of individual release assets.
    /// If the path is a directory, it will be zipped before being uploaded to the release.
    /// </summary>
    /// <param name="path">The absolute path to the file or directory to include as a release asset.</param>
    public static async Task AddReleaseAsset(AbsolutePath path)
    {
        var releaseAssetsDir = TemporaryDirectory / "release_assets";
        var assetOutDir = releaseAssetsDir / "assets";
        assetOutDir.CreateDirectory();
        if (path.FileExists())
        {
            await path.CopyTo(assetOutDir / path.Name);
        }
        else if (path.DirectoryExists())
        {
            var destinationPath = assetOutDir / (path.Name + ".zip");
            if (destinationPath.FileExists())
            {
                destinationPath.DeleteFile();
            }
            path.ZipTo(destinationPath);
        }
        Log.Information("Added {file} to release assets", path);
    }

    /// <summary>
    /// Adds a file or directory path to the collection of common release assets.
    /// These assets will be bundled together with other common assets from the same app ID into a single zip archive.
    /// </summary>
    /// <param name="path">The absolute path to the file or directory to include as a common release asset.</param>
    public static async Task AddReleaseCommonAsset(AbsolutePath path)
    {
        var releaseAssetsDir = TemporaryDirectory / "release_assets";
        var commonAssetOutDir = releaseAssetsDir / "common_assets";
        commonAssetOutDir.CreateDirectory();
        if (path.FileExists() || path.DirectoryExists())
        {
            await path.CopyTo(commonAssetOutDir);
            Log.Information("Added {file} to common assets", path);
        }
    }
}
