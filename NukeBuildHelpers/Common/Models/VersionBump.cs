using NukeBuildHelpers.Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Common.Models;

internal class VersionBump
{
    public required VersionPart Part { get; set; }

    public bool IsIncrement { get; set; }

    public int BumpAssign { get; set; }

    public int Rank { get; set; }
}
