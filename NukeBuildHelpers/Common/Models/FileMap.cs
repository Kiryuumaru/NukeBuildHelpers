using Nuke.Common.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Common.Models;

internal class FileMap
{
    public required AbsolutePath Source { get; init; }

    public required AbsolutePath[] Files { get; init; }

    public required AbsolutePath[] Folders { get; init; }

    public required (AbsolutePath Link, AbsolutePath Target)[] SymbolicLinks { get; init; }
}
