using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Enums;

public enum BuildsOnType
{
    [EnumMember(Value = "windows-2022")]
    Windows2022,

    [EnumMember(Value = "ubuntu-22.04")]
    Ubuntu2204
}
