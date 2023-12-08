using Nuke.Common.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Models;

public class CIConfig<TConfig>
{
    public TConfig Config { get; set; }

    public JsonNode Json { get; set; }

    public AbsolutePath AbsolutePath { get; set; }
}
