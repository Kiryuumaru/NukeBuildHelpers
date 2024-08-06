using Nuke.Common.IO;
using Nuke.Common.Utilities.Collections;
using NukeBuildHelpers.Common.Models;
using Serilog;

namespace NukeBuildHelpers.Common;

/// <summary>
/// Additional convenient methods for <see cref="AbsolutePath"/>.
/// </summary>
public static class AbsolutePathExtensions
{
    /// <summary>
    /// Determines whether the current path is a parent of the specified child path.
    /// </summary>
    /// <param name="parent">The parent path.</param>
    /// <param name="child">The child path.</param>
    /// <returns>True if the current path is a parent of the specified child path, otherwise false.</returns>
    public static bool IsParentOf(this AbsolutePath parent, AbsolutePath child)
    {
        var pathToCheck = child.Parent;

        while (pathToCheck != null)
        {
            if (pathToCheck == parent)
            {
                return true;
            }

            pathToCheck = pathToCheck.Parent;
        }

        return false;
    }

    /// <summary>
    /// Determines whether the current path is a parent or the same as the specified child path.
    /// </summary>
    /// <param name="parent">The parent path.</param>
    /// <param name="child">The child path.</param>
    /// <returns>True if the current path is a parent or the same as the specified child path, otherwise false.</returns>
    public static bool IsParentOrSelfOf(this AbsolutePath parent, AbsolutePath child)
    {
        if (parent == child)
        {
            return true;
        }

        return IsParentOf(parent, child);
    }

    /// <summary>
    /// Determines whether the current path is a child of the specified parent path.
    /// </summary>
    /// <param name="child">The child path.</param>
    /// <param name="parent">The parent path.</param>
    /// <returns>True if the current path is a child of the specified parent path, otherwise false.</returns>
    public static bool IsChildOf(this AbsolutePath child, AbsolutePath parent)
    {
        return IsParentOf(parent, child);
    }

    /// <summary>
    /// Determines whether the current path is a child or the same as the specified parent path.
    /// </summary>
    /// <param name="child">The child path.</param>
    /// <param name="parent">The parent path.</param>
    /// <returns>True if the current path is a child or the same as the specified parent path, otherwise false.</returns>
    public static bool IsChildOrSelfOf(this AbsolutePath child, AbsolutePath parent)
    {
        return IsParentOrSelfOf(parent, child);
    }

    /// <summary>
    /// Recursively copies all files and directories from the specified path to the target path.
    /// </summary>
    /// <param name="path">The source path.</param>
    /// <param name="targetPath">The target path.</param>
    /// <returns>A task representing the asynchronous copy operation.</returns>
    public static async Task CopyRecursively(this AbsolutePath path, AbsolutePath targetPath)
    {
        if (path.FileExists())
        {
            Directory.CreateDirectory(targetPath.Parent);
            File.Copy(path.ToString(), targetPath.ToString(), true);
        }
        else if (path.DirectoryExists())
        {
            var fileMap = GetFileMap(path);

            Directory.CreateDirectory(targetPath);
            foreach (var folder in fileMap.Folders)
            {
                AbsolutePath target = folder.ToString().Replace(path, targetPath);
                Directory.CreateDirectory(target);
            }
            foreach (var file in fileMap.Files)
            {
                AbsolutePath target = file.ToString().Replace(path, targetPath);
                Directory.CreateDirectory(target.Parent);
                File.Copy(file, target, true);
            }
            foreach (var (Link, Target) in fileMap.SymbolicLinks)
            {
                AbsolutePath newLink = Link.ToString().Replace(path, targetPath);
                AbsolutePath newTarget;
                if (path.IsParentOf(Target))
                {
                    newTarget = Target.ToString().Replace(path, targetPath);
                }
                else
                {
                    newTarget = Target;
                }

                await newLink.DeleteRecursively();
                Directory.CreateDirectory(newLink.Parent);

                if (Target.DirectoryExists() || Link.DirectoryExists())
                {
                    Directory.CreateSymbolicLink(newLink, newTarget);
                }
                else
                {
                    File.CreateSymbolicLink(newLink, newTarget);
                }
            }
        }
    }

    /// <summary>
    /// Recursively moves all files and directories from the specified path to the target path.
    /// </summary>
    /// <param name="path">The source path.</param>
    /// <param name="targetPath">The target path.</param>
    /// <returns>A task representing the asynchronous move operation.</returns>
    public static async Task MoveRecursively(this AbsolutePath path, AbsolutePath targetPath)
    {
        if (path.FileExists())
        {
            Directory.CreateDirectory(targetPath.Parent);
            File.Move(path.ToString(), targetPath.ToString(), true);
        }
        else if (path.DirectoryExists())
        {
            var fileMap = GetFileMap(path);

            Directory.CreateDirectory(targetPath);
            foreach (var folder in fileMap.Folders)
            {
                AbsolutePath target = folder.ToString().Replace(path, targetPath);
                Directory.CreateDirectory(target);
            }
            foreach (var file in fileMap.Files)
            {
                AbsolutePath target = file.ToString().Replace(path, targetPath);
                Directory.CreateDirectory(target.Parent);
                File.Move(file, target, true);
            }
            foreach (var (Link, Target) in fileMap.SymbolicLinks)
            {
                AbsolutePath newLink = Link.ToString().Replace(path, targetPath);
                AbsolutePath newTarget;
                if (path.IsParentOf(Target))
                {
                    newTarget = Target.ToString().Replace(path, targetPath);
                }
                else
                {
                    newTarget = Target;
                }

                await newLink.DeleteRecursively();
                Directory.CreateDirectory(newLink.Parent);

                if (Target.DirectoryExists() || Link.DirectoryExists())
                {
                    Directory.CreateSymbolicLink(newLink, newTarget);
                }
                else
                {
                    File.CreateSymbolicLink(newLink, newTarget);
                }
            }

            await path.DeleteRecursively();
        }
    }

    /// <summary>
    /// Recursively deletes all files and directories from the specified path.
    /// </summary>
    /// <param name="path">The source path to delete.</param>
    /// <returns>A task representing the asynchronous move operation.</returns>
    public static Task DeleteRecursively(this AbsolutePath path)
    {
        return Task.Run(() =>
        {
            if (path.FileExists())
            {
                File.Delete(path);
            }
            else if (path.DirectoryExists())
            {
                var fileMap = GetFileMap(path);

                foreach (var (Link, Target) in fileMap.SymbolicLinks)
                {
                    if (Link.DirectoryExists())
                    {
                        Directory.Delete(Link);
                    }
                    else
                    {
                        File.Delete(Link);
                    }
                }

                Directory.Delete(path, true);
            }
        });
    }

    private static FileMap GetFileMap(AbsolutePath path)
    {
        List<AbsolutePath> files = [];
        List<AbsolutePath> folders = [];
        List<(AbsolutePath Link, AbsolutePath Target)> symbolicLinks = [];

        List<AbsolutePath> next = [path];

        while (!next.IsEmpty())
        {
            List<AbsolutePath> forNext = [];

            foreach (var item in next)
            {
                bool hasNext = false;

                if (item.FileExists())
                {
                    FileInfo fileInfo = new(item);
                    if (fileInfo.LinkTarget != null)
                    {
                        string linkTarget;
                        if (!Path.IsPathRooted(fileInfo.LinkTarget))
                        {
                            linkTarget = item.Parent / fileInfo.LinkTarget;
                            Log.Information("fil1 linkTarget: {linkTarget}", linkTarget);
                        }
                        else
                        {
                            linkTarget = fileInfo.LinkTarget;
                            Log.Information("fil2 linkTarget: {linkTarget}", linkTarget);
                        }
                        symbolicLinks.Add((item, linkTarget));
                    }
                    else
                    {
                        files.Add(item);
                    }
                }
                else if (item.DirectoryExists())
                {
                    DirectoryInfo directoryInfo = new(item);
                    if (directoryInfo.LinkTarget != null)
                    {
                        string linkTarget;
                        if (!Path.IsPathRooted(directoryInfo.LinkTarget))
                        {
                            linkTarget = item.Parent / directoryInfo.LinkTarget;
                            Log.Information("dir1 linkTarget: {linkTarget}", linkTarget);
                        }
                        else
                        {
                            linkTarget = directoryInfo.LinkTarget;
                            Log.Information("dir2 linkTarget: {linkTarget}", linkTarget);
                        }
                        symbolicLinks.Add((item, linkTarget));
                    }
                    else
                    {
                        folders.Add(item);

                        hasNext = true;
                    }
                }

                if (hasNext)
                {
                    try
                    {
                        forNext.AddRange(Directory.GetFiles(item, "*", SearchOption.TopDirectoryOnly).Select(AbsolutePath.Create));
                    }
                    catch { }
                    try
                    {
                        forNext.AddRange(Directory.GetDirectories(item, "*", SearchOption.TopDirectoryOnly).Select(AbsolutePath.Create));
                    }
                    catch { }
                }
            }

            next = forNext;
        }

        List<(AbsolutePath Link, AbsolutePath Target)> arangedSymbolicLinks = [];
        foreach (var symbolicLink in symbolicLinks)
        {
            bool add = true;
            foreach (var arangedSymbolicLink in new List<(AbsolutePath Link, AbsolutePath Target)>(arangedSymbolicLinks))
            {
                if (symbolicLink.Target == arangedSymbolicLink.Link)
                {
                    arangedSymbolicLinks.Insert(arangedSymbolicLinks.IndexOf(arangedSymbolicLink) + 1, symbolicLink);
                    add = false;
                    break;
                }
                else if (symbolicLink.Link == arangedSymbolicLink.Target)
                {
                    arangedSymbolicLinks.Insert(arangedSymbolicLinks.IndexOf(arangedSymbolicLink), symbolicLink);
                    add = false;
                    break;
                }
            }
            if (add)
            {
                arangedSymbolicLinks.Add(symbolicLink);
            }
        }

        return new()
        {
            Source = path,
            Files = [.. files],
            Folders = [.. folders],
            SymbolicLinks = [.. arangedSymbolicLinks]
        };
    }
}
