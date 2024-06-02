using Nuke.Common;
using Nuke.Common.Tooling;
using NukeBuildHelpers.Common;
using Serilog;

namespace NukeBuildHelpers;

partial class BaseNukeBuildHelpers
{
    public Target DeleteOriginTags => _ => _
        .Unlisted()
        .Description("Delete all origin tags, with --args \"{appid}\"")
        .Executes(() =>
        {
            CheckEnvironementBranches();

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
                ValueHelpers.GetOrFail(() => SplitArgs, out var splitArgs);
                ValueHelpers.GetOrFail(() => AppEntryHelpers.GetAppConfig(), out var appConfig);

                IReadOnlyCollection<Output>? lsRemote = null;

                foreach (var key in splitArgs.Keys.Any() ? splitArgs.Keys.ToList() : [""])
                {
                    string appId = key;

                    ValueHelpers.GetOrFail(appId, appConfig.AppEntryConfigs, out appId, out var appEntry);
                    ValueHelpers.GetOrFail(() => AppEntryHelpers.GetAllVersions(this, appId, appConfig.AppEntryConfigs, ref lsRemote), out var allVersions);

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
