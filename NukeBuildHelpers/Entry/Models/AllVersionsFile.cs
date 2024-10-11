using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Entry.Models;

internal class AllVersionsFile
{
    public required Dictionary<string, VersionFile> Versions { get; init; }
}
