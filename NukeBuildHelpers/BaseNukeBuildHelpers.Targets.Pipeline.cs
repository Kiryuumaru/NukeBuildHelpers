using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
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

partial class BaseNukeBuildHelpers
{
    public Target GenerateVersionFiles => _ => _
        .Description("Generates version files from tags, with --args \"path={path};{appid}\"")
        .Executes(() => {
            GetOrFail(() => SplitArgs, out var splitArgs);
            GetOrFail(() => GetAppEntryConfigs(), out var appEntryConfigs);

            splitArgs.TryGetValue("appid", out string appId);

            GetOrFail(appId, appEntryConfigs, out appId, out var appEntry);
            GetOrFail(() => GetAllVersions(appId, appEntryConfigs), out var allVersions);

            splitArgs.TryGetValue("path", out string pathRaw);


        });

    public Target GithubPublish => _ => _
        .Description("Generates app test entry template, with --args \"path={path}\"")
        .Executes(() => {
            GetOrFail(() => SplitArgs, out var splitArgs);

            splitArgs.TryGetValue("path", out string pathRaw);

            AbsolutePath absolutePath = RootDirectory / "apptestentry.sample.json";
            if (!string.IsNullOrEmpty(pathRaw))
            {
                absolutePath = AbsolutePath.Create(absolutePath);
            }

            Log.Information("Generating app config to \"{path}\"", absolutePath);

            //AppTestEntryConfig config = new()
            //{
            //    BuildsOn = Enums.BuildsOnType.Ubuntu2204
            //};

            //File.WriteAllText(absolutePath, JsonSerializer.Serialize(config, jsonSerializerOptions));

            Log.Information("Generate done");
        });
}
