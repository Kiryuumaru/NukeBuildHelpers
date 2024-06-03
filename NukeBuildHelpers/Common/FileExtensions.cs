using Nuke.Common.IO;

namespace NukeBuildHelpers.Common;

internal static class AbsolutePathExtensions
{
    public static void CopyFilesRecursively(this AbsolutePath path, AbsolutePath targetPath)
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

    public static void MoveFilesRecursively(this AbsolutePath path, AbsolutePath targetPath)
    {
        var source = path.ToString().TrimEnd('\\', ' ');
        var target = targetPath.ToString().TrimEnd('\\', ' ');
        var files = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
                             .GroupBy(Path.GetDirectoryName);
        foreach (var folder in files)
        {
            var targetFolder = folder.Key!.Replace(source, target);
            Directory.CreateDirectory(targetFolder);
            foreach (var file in folder)
            {
                var targetFile = Path.Combine(targetFolder, Path.GetFileName(file));
                if (File.Exists(targetFile)) File.Delete(targetFile);
                File.Move(file, targetFile);
            }
        }
        Directory.Delete(source, true);
    }
}
