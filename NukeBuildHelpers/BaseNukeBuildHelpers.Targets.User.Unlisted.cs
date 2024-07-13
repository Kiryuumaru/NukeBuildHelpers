using Nuke.Common;
using Nuke.Common.Tooling;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Entry.Helpers;
using Serilog;

namespace NukeBuildHelpers;

partial class BaseNukeBuildHelpers
{
    /// <summary>
    /// Deletes all origin tags, with --args "{appid}".
    /// </summary>
    public Target DeleteOriginTags => _ => _
        .Unlisted()
        .Description("Delete all origin tags, with --args \"{appid}\"")
        .Executes(async () =>
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

                var allEntry = await ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this));

                IReadOnlyCollection<Output>? lsRemote = null;

                foreach (var key in splitArgs.Keys.Any() ? splitArgs.Keys.ToList() : [""])
                {
                    string appId = key;

                    ValueHelpers.GetOrFail(appId, allEntry, out var appEntry);
                    ValueHelpers.GetOrFail(() => EntryHelpers.GetAllVersions(this, appId, ref lsRemote), out var allVersions);

                    tagsToDelete.AddRange(allVersions.VersionCommitPaired.Select(i => appId + "/" + i.Key.ToString()));
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
