using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using NukeBuildHelpers.Enums;

namespace NukeBuildHelpers.Models;

public class AppTestEntryConfig
{
    public bool Enable { get; set; }

    public string[] AppEntryIds { get; set; }

    public string Name { get; set; }

    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    public BuildsOnType BuildsOn { get; set; }
}
