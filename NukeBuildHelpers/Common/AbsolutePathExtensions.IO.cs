using Nuke.Common.IO;

namespace NukeBuildHelpers.Common;

public static partial class AbsolutePathExtensions
{
    internal class FileMap(AbsolutePath source, AbsolutePath[] files, AbsolutePath[] folders, (AbsolutePath Link, AbsolutePath Target)[] symbolicLinks)
    {
        public AbsolutePath Source { get; } = source;

        public AbsolutePath[] Files { get; } = files;

        public AbsolutePath[] Folders { get; } = folders;

        public (AbsolutePath Link, AbsolutePath Target)[] SymbolicLinks { get; } = symbolicLinks;
    }
}
