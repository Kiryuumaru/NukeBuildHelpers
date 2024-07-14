using Nuke.Common.IO;
using Serilog;

namespace NukeBuildHelpers.Common;

internal static class AbsolutePathExtensions
{
    public static void CopyFilesRecursively(this AbsolutePath path, AbsolutePath targetPath)
    {
        if (path.FileExists())
        {
            Directory.CreateDirectory(targetPath.Parent);
            File.Copy(path.ToString(), targetPath.ToString(), true);
        }
        else
        {
            foreach (string dirPath in Directory.GetDirectories(path.ToString(), "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(path.ToString(), targetPath));
            }
            foreach (string newPath in Directory.GetFiles(path.ToString(), "*.*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(AbsolutePath.Create(newPath.Replace(path.ToString(), targetPath.ToString())).Parent);
                File.Copy(newPath, newPath.Replace(path.ToString(), targetPath.ToString()), true);
            }
        }
    }

    public static void MoveFilesRecursively(this AbsolutePath path, AbsolutePath targetPath)
    {
        if (path.FileExists())
        {
            Directory.CreateDirectory(targetPath.Parent);
            File.Move(path.ToString(), targetPath.ToString(), true);
        }
        else
        {
            List<AbsolutePath> linkedPaths = [];
            foreach (AbsolutePath dirPath in Directory.GetDirectories(path.ToString(), "*", SearchOption.AllDirectories))
            {
                if (new DirectoryInfo(dirPath).LinkTarget != null && !linkedPaths.Any(i => dirPath == i || (dirPath.ToString().StartsWith(i) && dirPath.Parent != i.Parent)))
                {
                    linkedPaths.Add(dirPath);
                }
                Directory.CreateDirectory(dirPath.ToString().Replace(path, targetPath));
            }
            foreach (AbsolutePath linkedPath in linkedPaths)
            {
                var linkTarget = new DirectoryInfo(linkedPath).LinkTarget!;
                if (!Path.IsPathRooted(linkTarget))
                {
                    linkTarget = Path.GetFullPath(new DirectoryInfo(linkedPath).LinkTarget!, linkedPath.Parent);
                }
                string linkTargetPath = linkedPath.ToString().Replace(linkedPath.Parent, targetPath);
                foreach (string newPath in Directory.GetFiles(linkTarget, "*.*", SearchOption.AllDirectories))
                {
                    File.Copy(newPath, newPath.Replace(linkTarget, linkTargetPath), true);
                }
            }
            foreach (AbsolutePath newPath in Directory.GetFiles(path.ToString(), "*.*", SearchOption.AllDirectories))
            {
                if (!linkedPaths.Any(i => newPath == i || (newPath.ToString().StartsWith(i) && newPath.Parent != i.Parent)))
                {
                    Directory.CreateDirectory(AbsolutePath.Create(newPath.ToString().Replace(path.ToString(), targetPath.ToString())).Parent);
                    File.Move(newPath, newPath.ToString().Replace(path.ToString(), targetPath.ToString()), true);
                }
            }
            foreach (AbsolutePath linkedPath in linkedPaths)
            {
                Directory.Delete(linkedPath);
            }
            Directory.Delete(path, true);
        }
    }
}
