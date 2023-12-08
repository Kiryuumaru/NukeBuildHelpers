using System;
using System.Linq;
using Serilog;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using Nuke.Common.Git;
using System.Collections.Generic;
using System.Text.Json;
using System.IO;
using Nuke.Common.Tools.GitVersion;

namespace NukeBuildHelpers;

partial class BaseNukeBuild : NukeBuild
{
    [GitRepository]
    protected readonly GitRepository Repository;

    [GitVersion]
    protected readonly GitVersion GitVersion;

    [PathVariable]
    protected readonly Tool Git;

    static readonly JsonSerializerOptions jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}
