using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Utilities.Collections;
using NukeBuildHelpers;
using Serilog;
using NukeBuildHelpers.Models;
using Nuke.Common.Tools.DotNet;
using System.Threading.Tasks;

public partial class Build : BaseNukeBuildHelpers
{
    public static int Main () => Execute<Build>(x => x.Version);

    [Solution(GenerateProjects = true)]
    internal readonly Solution Solution;

    [Parameter(Name = "NUGET_AUTH_TOKEN")][Secret] internal readonly string NuGetAuthToken;

    [Parameter(Name = "GITHUB_TOKEN")][Secret] internal readonly string GithubToken;
}
