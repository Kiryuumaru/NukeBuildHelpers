using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Enums;

[Flags]
public enum RunTestType
{
    None = 0b00,
    Local = 0b01,
    Target = 0b10,
    All = 0b11,
}
