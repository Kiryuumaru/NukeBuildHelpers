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
    /// <param name="customFilename">The custom filename of the asset for release</param>
    public static async Task AddReleaseAsset(AbsolutePath path, string? customFilename = null)
    {
        var releaseAssetsDir = CommonOutputDirectory / "$common";
        var assetOutDir = releaseAssetsDir / "assets";
        assetOutDir.CreateDirectory();
        if (path.FileExists())
        {
            var name = string.IsNullOrWhiteSpace(customFilename) ? path.Name : customFilename;
            await path.CopyTo(assetOutDir / name);
            Log.Information("Added file {file} as {name} to release assets", path, name);
        }
        else if (path.DirectoryExists())
        {
            var name = (string.IsNullOrWhiteSpace(customFilename) ? path.Name : customFilename) + ".zip";
            var destinationPath = assetOutDir / name;
            if (destinationPath.FileExists())
            {
                destinationPath.DeleteFile();
            }
            path.ZipTo(destinationPath);
            Log.Information("Added archive {file} as {name} to release assets", path, name);
        }
        else
        {
            Log.Warning("Asset {file} does not exists", path);
        }
        foreach (var path1 in releaseAssetsDir.GetFiles("**", 99))
        {
            Log.Information(path1);
        }
    }
}
