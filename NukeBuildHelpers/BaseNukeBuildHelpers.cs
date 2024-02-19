using ICSharpCode.SharpZipLib.Zip;
using Nuke.Common;
using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using NukeBuildHelpers.Models;
using Octokit;
using Semver;
using Serilog;
using System.Text.Json;

namespace NukeBuildHelpers;

public abstract partial class BaseNukeBuildHelpers : NukeBuild, INukeBuildHelpers
{
    public static AbsolutePath OutputPath => RootDirectory / "nuke_output";

    GitRepository Repository => (this as INukeBuildHelpers).Repository;

    string Args => (this as INukeBuildHelpers).Args;

    Tool Git => (this as INukeBuildHelpers).Git;

    private IReadOnlyDictionary<string, string> splitArgs;
    public IReadOnlyDictionary<string, string> SplitArgs
    {
        get
        {
            if (splitArgs == null)
            {
                Dictionary<string, string> targetParams = new();
                if ((this as INukeBuildHelpers).Args != null)
                {
                    foreach (var targetParam in (this as INukeBuildHelpers).Args.Split(';'))
                    {
                        if (string.IsNullOrEmpty(targetParam))
                        {
                            continue;
                        }
                        try
                        {
                            var split = targetParam.Split('=');
                            targetParams.Add(split[0], split[1]);
                        }
                        catch
                        {
                            targetParams.Add(targetParam, null);
                        }
                    }
                }

                splitArgs = targetParams;
            }

            return splitArgs;
        }
    }
}
