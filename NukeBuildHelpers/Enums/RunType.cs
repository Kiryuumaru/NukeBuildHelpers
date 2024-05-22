using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Enums;

[Flags]
public enum RunType
{
    None = 0b0000,
    Local = 0b0001,
    PullRequest = 0b0010,
    Commit = 0b0100,
    Bump = 0b1000,
    All = 0b1111
}
