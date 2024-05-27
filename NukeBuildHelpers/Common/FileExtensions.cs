using Nuke.Common.IO;

namespace NukeBuildHelpers.Common;

internal static class AbsolutePathExtensions
{
    public static void CopyFilesRecursively(this AbsolutePath path, AbsolutePath targetPath)
    {
        //Now Create all of the directories
        foreach (string dirPath in Directory.GetDirectories(path.ToString(), "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(dirPath.Replace(path.ToString(), targetPath));
        }

        //Copy all the files & Replaces any files with the same name
        foreach (string newPath in Directory.GetFiles(path.ToString(), "*.*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(AbsolutePath.Create(newPath.Replace(path.ToString(), targetPath.ToString())).Parent);
            File.Copy(newPath, newPath.Replace(path.ToString(), targetPath.ToString()), true);
        }
    }
}
