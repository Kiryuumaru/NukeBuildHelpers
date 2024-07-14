using Nuke.Common.IO;

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
            foreach (string dirPath in Directory.GetDirectories(path.ToString(), "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(path.ToString(), targetPath));
            }

            foreach (string newPath in Directory.GetFiles(path.ToString(), "*.*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(AbsolutePath.Create(newPath.Replace(path.ToString(), targetPath.ToString())).Parent);
                File.Move(newPath, newPath.Replace(path.ToString(), targetPath.ToString()), true);
            }
            Directory.Delete(path, true);
        }
    }
}
