using Nuke.Common.IO;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

#if NETSTANDARD
#elif NET5_0_OR_GREATER
using static NukeBuildHelpers.Common.Internals.Message;
#endif

namespace NukeBuildHelpers.Common;

public static partial class AbsolutePathExtensions
{
    /// <summary>
    /// Provides extension methods for the <see cref="AbsolutePath"/> class.
    /// </summary>
    public static FileInfo? ToFileInfo(this AbsolutePath absolutePath)
    {
        return absolutePath is not null ? new FileInfo(absolutePath) : null;
    }

    /// <summary>
    /// Converts the <see cref="AbsolutePath"/> to a <see cref="FileInfo"/> object.
    /// </summary>
    /// <param name="absolutePath">The absolute path to convert.</param>
    /// <returns>A <see cref="FileInfo"/> object representing the file, or null if the path is null.</returns>
    public static DirectoryInfo? ToDirectoryInfo(this AbsolutePath absolutePath)
    {
        return absolutePath is not null ? new DirectoryInfo(absolutePath) : null;
    }

    /// <summary>
    /// Checks if the directory specified by the <see cref="AbsolutePath"/> contains any files matching the specified pattern.
    /// </summary>
    /// <param name="absolutePath">The absolute path of the directory.</param>
    /// <param name="pattern">The search string to match against the names of files.</param>
    /// <param name="options">Specifies whether the search operation should include only the current directory or all subdirectories.</param>
    /// <returns>True if the directory contains files matching the pattern, otherwise false.</returns>
    public static bool ContainsFile(this AbsolutePath absolutePath, string pattern, SearchOption options = SearchOption.TopDirectoryOnly)
    {
        return ToDirectoryInfo(absolutePath)?.GetFiles(pattern, options).Length != 0;
    }

    /// <summary>
    /// Checks if the directory specified by the <see cref="AbsolutePath"/> contains any directories matching the specified pattern.
    /// </summary>
    /// <param name="absolutePath">The absolute path of the directory.</param>
    /// <param name="pattern">The search string to match against the names of directories.</param>
    /// <param name="options">Specifies whether the search operation should include only the current directory or all subdirectories.</param>
    /// <returns>True if the directory contains directories matching the pattern, otherwise false.</returns>
    public static bool ContainsDirectory(this AbsolutePath absolutePath, string pattern, SearchOption options = SearchOption.TopDirectoryOnly)
    {
        return ToDirectoryInfo(absolutePath)?.GetDirectories(pattern, options).Length != 0;
    }

    /// <summary>
    /// Retrieves all files and directories within the directory specified by the <see cref="AbsolutePath"/>.
    /// </summary>
    /// <param name="absolutePath">The absolute path of the directory.</param>
    /// <returns>An enumerable collection of <see cref="AbsolutePath"/> objects representing the files and directories.</returns>
    public static IEnumerable<AbsolutePath> GetPaths(this AbsolutePath absolutePath)
    {
        var paths = new List<AbsolutePath>();
        paths.AddRange(absolutePath.GetFiles(absolutePath));
        paths.AddRange(absolutePath.GetDirectories(absolutePath));
        return paths;
    }

    /// <summary>
    /// Reads and deserializes a JSON file at the specified <see cref="AbsolutePath"/> into an object of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the object to deserialize to.</typeparam>
    /// <param name="absolutePath">The absolute path of the file.</param>
    /// <param name="jsonSerializerOptions">Options to control the behavior during deserialization.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the read operation.</param>
    /// <returns>A task that represents the asynchronous read operation. The task result contains the deserialized object.</returns>
    [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
    public static async Task<T?> Read<T>(this AbsolutePath absolutePath, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
    {
        return JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(absolutePath, cancellationToken), jsonSerializerOptions);
    }

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

    private static FileMap GetFileMap(AbsolutePath path)
    {
        List<AbsolutePath> files = [];
        List<AbsolutePath> folders = [];
        List<(AbsolutePath Link, AbsolutePath Target)> symbolicLinks = [];

        List<AbsolutePath> next = [path];

        while (next.Count != 0)
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
                            linkTarget = item.Parent! / fileInfo.LinkTarget;
                        }
                        else
                        {
                            linkTarget = fileInfo.LinkTarget;
                        }
                        if (linkTarget.StartsWith("\\??\\"))
                        {
                            linkTarget = linkTarget[4..];
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
                            linkTarget = item.Parent! / directoryInfo.LinkTarget;
                        }
                        else
                        {
                            linkTarget = directoryInfo.LinkTarget;
                        }
                        if (linkTarget.StartsWith("\\??\\"))
                        {
#if NETSTANDARD
                            linkTarget = linkTarget.Substring(4);
#elif NET5_0_OR_GREATER
                            linkTarget = linkTarget[4..];
#endif
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

        return new(path, [.. files], [.. folders], [.. arangedSymbolicLinks]);
    }
}
