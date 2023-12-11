using Nuke.Common.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Models;

public class AppConfig
{
    public JsonNode Json { get; set; }

    public AbsolutePath AbsolutePath { get; set; }
}

public class AppConfig<TConfig> : AppConfig
{
    public TConfig Config { get; set; }
}
