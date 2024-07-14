using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Utilities.Collections;
using Serilog;
using System.Linq;

namespace NukeBuildHelpers.Common;

/// <summary>
/// Additional convenient methods for <see cref="AbsolutePath"/>.
/// </summary>
public static class AbsolutePathExtensions
{
    private static List<(AbsolutePath AbsolutePath, bool FromLink)> SafeGetAllFilesAndDirectories(AbsolutePath path)
    {
        List<(AbsolutePath AbsolutePath, bool FromLink)> nextPaths = [(path, new DirectoryInfo(path).LinkTarget != null)];
        List<(AbsolutePath AbsolutePath, bool FromLink)> allFilesAndDirectories = [];
        List<AbsolutePath> linkedPaths = [];
        while (!nextPaths.IsEmpty())
        {
            List<(AbsolutePath AbsolutePath, bool FromLink)> forNextPaths = [];

            foreach (var item in nextPaths)
            {
                DirectoryInfo directoryInfo = new(item.AbsolutePath);
                bool fromLink = item.FromLink;
                if (directoryInfo.LinkTarget != null)
                {
                    var linkTarget = directoryInfo.LinkTarget;
                    if (!Path.IsPathRooted(linkTarget))
                    {
                        linkTarget = Path.GetFullPath(linkTarget, item.AbsolutePath.Parent);
                    }

                    if (linkedPaths.Any(i => linkTarget == i))
                    {
                        continue;
                    }

                    linkedPaths.Add(item.AbsolutePath);

                    fromLink = true;
                }

                allFilesAndDirectories.Add((item.AbsolutePath, item.FromLink));

                try
                {
                    allFilesAndDirectories.AddRange(Directory.GetFiles(item.AbsolutePath, "*", SearchOption.TopDirectoryOnly).Select(AbsolutePath.Create)
                        .Select(i => (i, fromLink)));
                }
                catch { }

                try
                {
                    forNextPaths.AddRange(Directory.GetDirectories(item.AbsolutePath, "*", SearchOption.TopDirectoryOnly).Select(AbsolutePath.Create)
                        .Select(i => (i, fromLink)));
                }
                catch { }
            }

            nextPaths = forNextPaths;
        }

        return allFilesAndDirectories;
    }

    /// <summary>
    /// Recursively copies all files and directories from the specified path to the target path.
    /// </summary>
    /// <param name="path">The source path.</param>
    /// <param name="targetPath">The target path.</param>
    /// <returns>A task representing the asynchronous copy operation.</returns>
    public static Task CopyFilesRecursively(this AbsolutePath path, AbsolutePath targetPath)
    {
        return Task.Run(() =>
        {
            if (path.FileExists())
            {
                Directory.CreateDirectory(targetPath.Parent);
                File.Copy(path.ToString(), targetPath.ToString(), true);
            }
            else
            {
                foreach (var safePath in SafeGetAllFilesAndDirectories(path))
                {
                    var destinationPath = AbsolutePath.Create(safePath.AbsolutePath.ToString().Replace(path, targetPath));
                    if (safePath.AbsolutePath.FileExists())
                    {
                        Directory.CreateDirectory(destinationPath.Parent);
                        try
                        {
                            File.Copy(safePath.AbsolutePath, destinationPath, true);
                        }
                        catch { }
                    }
                    else if (safePath.AbsolutePath.DirectoryExists())
                    {
                        Directory.CreateDirectory(destinationPath);
                    }
                }
            }
        });
    }

    /// <summary>
    /// Recursively moves all files and directories from the specified path to the target path.
    /// </summary>
    /// <param name="path">The source path.</param>
    /// <param name="targetPath">The target path.</param>
    /// <returns>A task representing the asynchronous move operation.</returns>
    public static Task MoveFilesRecursively(this AbsolutePath path, AbsolutePath targetPath)
    {
        return Task.Run(() =>
        {
            if (path.FileExists())
            {
                Directory.CreateDirectory(targetPath.Parent);
                File.Move(path.ToString(), targetPath.ToString(), true);
            }
            else
            {
                var allFilesAndDirectories = SafeGetAllFilesAndDirectories(path);
                foreach (var safePath in allFilesAndDirectories)
                {
                    var destinationPath = AbsolutePath.Create(safePath.AbsolutePath.ToString().Replace(path, targetPath));
                    if (safePath.AbsolutePath.FileExists())
                    {
                        Directory.CreateDirectory(destinationPath.Parent);
                        try
                        {
                            File.Copy(safePath.AbsolutePath, destinationPath, true);
                        }
                        catch { }
                    }
                    else if (safePath.AbsolutePath.DirectoryExists())
                    {
                        Directory.CreateDirectory(destinationPath);
                    }
                }
                foreach (var safePath in allFilesAndDirectories)
                {
                    if (!safePath.FromLink && new DirectoryInfo(safePath.AbsolutePath).LinkTarget != null)
                    {
                        try
                        {
                            File.Delete(safePath.AbsolutePath);
                        }
                        catch { }
                        try
                        {
                            Directory.Delete(safePath.AbsolutePath);
                        }
                        catch { }
                    }
                }
                try
                {
                    Directory.Delete(path);
                }
                catch { }
                try
                {
                    Directory.Delete(path, true);
                }
                catch { }
                try
                {
                    Directory.Delete(path);
                }
                catch { }
                try
                {
                    Directory.Delete(path, true);
                }
                catch { }
            }
        });
    }

    /// <summary>
    /// Recursively deletes all files and directories from the specified path.
    /// </summary>
    /// <param name="path">The source path to delete.</param>
    /// <returns>A task representing the asynchronous move operation.</returns>
    public static Task DeleteFilesRecursively(this AbsolutePath path)
    {
        return Task.Run(() =>
        {
            if (path.FileExists())
            {
                File.Delete(path);
            }
            else
            {
                var allFilesAndDirectories = SafeGetAllFilesAndDirectories(path);
                foreach (var safePath in allFilesAndDirectories)
                {
                    if (!safePath.FromLink && new DirectoryInfo(safePath.AbsolutePath).LinkTarget != null)
                    {
                        try
                        {
                            File.Delete(safePath.AbsolutePath);
                        }
                        catch { }
                        try
                        {
                            Directory.Delete(safePath.AbsolutePath);
                        }
                        catch { }
                    }
                }
                try
                {
                    Directory.Delete(path);
                }
                catch { }
                try
                {
                    Directory.Delete(path, true);
                }
                catch { }
                try
                {
                    Directory.Delete(path);
                }
                catch { }
                try
                {
                    Directory.Delete(path, true);
                }
                catch { }
            }
        });
    }
}
