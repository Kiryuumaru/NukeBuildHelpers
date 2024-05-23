using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.DependencyModel;
using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Models;
using Octokit;
using Semver;
using Serilog;
using Serilog.Events;
using System.Reflection;
using System.Text.Json;
using YamlDotNet.Core.Tokens;

namespace NukeBuildHelpers;

partial class BaseNukeBuildHelpers
{
    public Target DeleteOriginTags => _ => _
        .Unlisted()
        .Description("Delete all origin tags, with --args \"{appid}\"")
        .Executes(() =>
        {
            List<string> tagsToDelete = [];
            if (string.IsNullOrEmpty(Args))
            {
                string basePeel = "refs/tags/";
                foreach (var refs in Git.Invoke("ls-remote -t -q", logOutput: false, logInvocation: false))
                {
                    string tag = refs.Text[(refs.Text.IndexOf(basePeel) + basePeel.Length)..];
                    tagsToDelete.Add(tag);
                }
            }
            else
            {
                GetOrFail(() => SplitArgs, out var splitArgs);
                GetOrFail(() => GetAppConfig(), out var appConfig);

                IReadOnlyCollection<Output>? lsRemote = null;

                foreach (var key in splitArgs.Keys.Any() ? splitArgs.Keys.ToList() : [""])
                {
                    string appId = key;

                    GetOrFail(appId, appConfig.AppEntryConfigs, out appId, out var appEntry);
                    GetOrFail(() => GetAllVersions(appId, appConfig.AppEntryConfigs, ref lsRemote), out var allVersions);

                    if (appEntry.Entry.MainRelease)
                    {
                        tagsToDelete.AddRange(allVersions.VersionCommitPaired.Select(i => i.Key.ToString()));
                    }
                    else
                    {
                        tagsToDelete.AddRange(allVersions.VersionCommitPaired.Select(i => appId + "/" + i.Key.ToString()));
                    }
                }
            }

            foreach (var tag in tagsToDelete)
            {
                Log.Information("Deleting tag {tag}...", tag);
                Git.Invoke("push origin :refs/tags/" + tag, logInvocation: false, logOutput: false);
            }

            Log.Information("Deleting tag done");
        });
}
