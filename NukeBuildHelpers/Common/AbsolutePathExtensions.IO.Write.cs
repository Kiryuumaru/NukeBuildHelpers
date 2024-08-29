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
    /// Asynchronously writes the specified object as JSON to the file at the given <see cref="AbsolutePath"/>.
    /// </summary>
    /// <typeparam name="T">The type of the object to write.</typeparam>
    /// <param name="absolutePath">The absolute path to the file.</param>
    /// <param name="obj">The object to write as JSON.</param>
    /// <param name="jsonSerializerOptions">Options to control the behavior during serialization.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the write operation.</param>
    [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
    public static async Task Write<T>(this AbsolutePath absolutePath, T obj, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
    {
        absolutePath.Parent?.CreateDirectory();
        await File.WriteAllTextAsync(absolutePath, JsonSerializer.Serialize(obj, jsonSerializerOptions), cancellationToken);
    }

    /// <summary>
    /// Creates a file at the specified <see cref="AbsolutePath"/> or updates its last write time.
    /// </summary>
    /// <param name="absolutePath">The absolute path to the file.</param>
    /// <param name="time">The time to set as the last write time. If null, the current time is used.</param>
    /// <param name="createDirectories">Whether to create parent directories if they do not exist.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous touch file operation.</returns>
    public static async Task TouchFile(this AbsolutePath absolutePath, DateTime? time = null, bool createDirectories = true, CancellationToken cancellationToken = default)
    {
        if (createDirectories)
        {
            absolutePath.Parent?.CreateDirectory();
        }

        if (!File.Exists(absolutePath))
        {
            await File.WriteAllBytesAsync(absolutePath, [], cancellationToken);
        }

        File.SetLastWriteTime(absolutePath, time ?? DateTime.Now);
    }

    /// <summary>
    /// Recursively copies all files and directories from the specified path to the target path.
    /// </summary>
    /// <param name="path">The source path.</param>
    /// <param name="targetPath">The target path.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous copy operation.</returns>
    public static async Task<bool> CopyTo(this AbsolutePath path, AbsolutePath targetPath, CancellationToken cancellationToken = default)
    {
        if (path.FileExists())
        {
            targetPath.Parent?.CreateDirectory();

            File.Copy(path.ToString(), targetPath.ToString(), true);

            return true;
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
                target.Parent?.CreateDirectory();
                File.Copy(file, target, true);
            }
            foreach (var (Link, Target) in fileMap.SymbolicLinks)
            {
                AbsolutePath newLink = Link.ToString().Replace(path, targetPath);
                string newTarget;
                if (path.IsParentOf(Target))
                {
                    newTarget = Target.ToString().Replace(path, targetPath);
                }
                else
                {
                    newTarget = Target;
                }

                await newLink.Delete(cancellationToken);
                newLink.Parent?.CreateDirectory();

                if (Target.DirectoryExists() || Link.DirectoryExists())
                {
                    Directory.CreateSymbolicLink(newLink, newTarget);
                }
                else
                {
                    File.CreateSymbolicLink(newLink, newTarget);
                }
            }

            return true;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// Recursively moves all files and directories from the specified path to the target path.
    /// </summary>
    /// <param name="path">The source path.</param>
    /// <param name="targetPath">The target path.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous move operation.</returns>
    public static async Task<bool> MoveTo(this AbsolutePath path, AbsolutePath targetPath, CancellationToken cancellationToken = default)
    {
        if (path.FileExists())
        {
            targetPath.Parent?.CreateDirectory();
            File.Move(path.ToString(), targetPath.ToString(), true);

            return true;
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
                target.Parent?.CreateDirectory();
                File.Move(file, target, true);
            }
            foreach (var (Link, Target) in fileMap.SymbolicLinks)
            {
                AbsolutePath newLink = Link.ToString().Replace(path, targetPath);
                string newTarget;
                if (path.IsParentOf(Target))
                {
                    newTarget = Target.ToString().Replace(path, targetPath);
                }
                else
                {
                    newTarget = Target;
                }

                await newLink.Delete(cancellationToken);
                newLink.Parent?.CreateDirectory();

                if (Target.DirectoryExists() || Link.DirectoryExists())
                {
                    Directory.CreateSymbolicLink(newLink, newTarget);
                }
                else
                {
                    File.CreateSymbolicLink(newLink, newTarget);
                }
            }

            await path.Delete(cancellationToken);

            return true;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// Recursively deletes all files and directories from the specified path.
    /// </summary>
    /// <param name="path">The source path to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous move operation.</returns>
    public static Task<bool> Delete(this AbsolutePath path, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (path.FileExists())
            {
                File.Delete(path);

                return true;
            }
            else if (path.DirectoryExists())
            {
                var fileMap = GetFileMap(path);

                foreach (var (Link, _) in fileMap.SymbolicLinks)
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

                return true;
            }
            else
            {
                return false;
            }
        }, cancellationToken);
    }
}
