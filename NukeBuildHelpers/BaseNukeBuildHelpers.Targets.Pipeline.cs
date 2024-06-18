using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Utilities.Collections;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Common.Models;
using NukeBuildHelpers.Entry.Helpers;
using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.Pipelines.Azure;
using NukeBuildHelpers.Pipelines.Common;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.Pipelines.Common.Models;
using NukeBuildHelpers.Pipelines.Github;
using Semver;
using Serilog;
using System.Text.Json;

namespace NukeBuildHelpers;

partial class BaseNukeBuildHelpers
{
    public Target PipelineTest => _ => _
        .Unlisted()
        .Description("To be used by pipeline")
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            ValueHelpers.GetOrFail(() => SplitArgs, out var splitArgs);
            ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this), out var allEntry);

            var pipeline = PipelineHelpers.SetupPipeline(this);

            await TestAppEntries(allEntry, pipeline, splitArgs.Select(i => i.Key), GetPipelinePreSetup());
        });

    public Target PipelineBuild => _ => _
        .Unlisted()
        .Description("To be used by pipeline")
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            ValueHelpers.GetOrFail(() => SplitArgs, out var splitArgs);
            ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this), out var allEntry);

            var pipeline = PipelineHelpers.SetupPipeline(this);

            await BuildAppEntries(allEntry, pipeline, splitArgs.Select(i => i.Key), GetPipelinePreSetup());
        });

    public Target PipelinePublish => _ => _
        .Unlisted()
        .Description("To be used by pipeline")
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            ValueHelpers.GetOrFail(() => SplitArgs, out var splitArgs);
            ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this), out var allEntry);

            var pipeline = PipelineHelpers.SetupPipeline(this);

            await PublishAppEntries(allEntry, pipeline, splitArgs.Select(i => i.Key), GetPipelinePreSetup());
        });

    public Target PipelinePreSetup => _ => _
        .Unlisted()
        .Description("To be used by pipeline")
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this), out var allEntry);

            var pipeline = PipelineHelpers.SetupPipeline(this);

            Log.Information("Target branch: {branch}", pipeline.PipelineInfo.Branch);
            Log.Information("Trigger type: {branch}", pipeline.PipelineInfo.TriggerType);

            await StartPreSetup(allEntry, pipeline);
        });

    public Target PipelinePostSetup => _ => _
        .Unlisted()
        .Description("To be used by pipeline")
        .Executes(() =>
        {
            CheckEnvironementBranches();

            ValueHelpers.GetOrFail(() => SplitArgs, out var splitArgs);
            ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this), out var allEntry);

            var preSetupOutput = GetPipelinePreSetup();

            if (preSetupOutput.HasRelease)
            {
                if (Environment.GetEnvironmentVariable("NUKE_PUBLISH_SUCCESS") == "ok")
                {
                    foreach (var release in OutputDirectory.GetDirectories())
                    {
                        if (!preSetupOutput.Entries.TryGetValue(release.Name, out var preSetupOutputVersion))
                        {
                            continue;
                        }
                        var outPath = OutputDirectory / release.Name + "-" + preSetupOutputVersion.Version;
                        var outPathZip = OutputDirectory / release.Name + "-" + preSetupOutputVersion.Version + ".zip";
                        release.CopyFilesRecursively(outPath);
                        outPath.ZipTo(outPathZip);
                    }
                    foreach (var release in OutputDirectory.GetFiles())
                    {
                        Log.Information("Publish: {name}", release.Name);
                    }

                    foreach (var release in preSetupOutput.Entries.Values)
                    {
                        if (!allEntry.AppEntryMap.TryGetValue(release.AppId, out var appEntry))
                        {
                            continue;
                        }
                        var version = SemVersion.Parse(release.Version, SemVersionStyles.Strict).WithoutMetadata();
                        string latestTag = "latest";
                        if (!release.Environment.Equals(MainEnvironmentBranch, StringComparison.InvariantCultureIgnoreCase))
                        {
                            latestTag += "-" + release.Environment.ToLowerInvariant();
                        }
                        if (appEntry.Entry.MainRelease)
                        {
                            Git.Invoke("tag -f " + version + "-passed");
                            Git.Invoke("tag -f " + version);
                            Git.Invoke("tag -f " + latestTag);
                        }
                        else
                        {
                            Git.Invoke("tag -f " + appEntry.Entry.Id.ToLowerInvariant() + "/" + version + "-passed");
                            Git.Invoke("tag -f " + appEntry.Entry.Id.ToLowerInvariant() + "/" + version);
                            Git.Invoke("tag -f " + appEntry.Entry.Id.ToLowerInvariant() + "/" + latestTag);
                        }
                    }

                    Git.Invoke("push -f --tags", logger: (s, e) => Log.Debug(e));

                    Gh.Invoke("release upload --clobber build." + preSetupOutput.BuildId + " " + string.Join(" ", OutputDirectory.GetFiles("*.zip").Select(i => i.ToString())));

                    Gh.Invoke("release edit --draft=false build." + preSetupOutput.BuildId);
                }
                else
                {
                    foreach (var release in preSetupOutput.Entries.Values)
                    {
                        if (!appConfig.AppEntryConfigs.TryGetValue(release.AppId, out var appEntry))
                        {
                            continue;
                        }
                        var version = SemVersion.Parse(release.Version, SemVersionStyles.Strict).WithoutMetadata();
                        string latestTag = "latest";
                        if (!release.Environment.Equals(MainEnvironmentBranch, StringComparison.InvariantCultureIgnoreCase))
                        {
                            latestTag += "-" + release.Environment.ToLowerInvariant();
                        }
                        if (appEntry.Entry.MainRelease)
                        {
                            Git.Invoke("tag -f " + version + "-failed");
                            Git.Invoke("tag -f " + version);
                        }
                        else
                        {
                            Git.Invoke("tag -f " + appEntry.Entry.Id.ToLowerInvariant() + "/" + version + "-failed");
                            Git.Invoke("tag -f " + appEntry.Entry.Id.ToLowerInvariant() + "/" + version);
                        }
                    }

                    Gh.Invoke("release delete -y build." + preSetupOutput.BuildId);

                    Git.Invoke("push -f --tags", logger: (s, e) => Log.Debug(e));
                }
            }
        });

    private static PipelinePreSetup GetPipelinePreSetup()
    {
        string? pipelinePreSetupValue = Environment.GetEnvironmentVariable("NUKE_PRE_SETUP");

        if (string.IsNullOrEmpty(pipelinePreSetupValue))
        {
            throw new Exception("NUKE_PRE_SETUP is empty");
        }

        PipelinePreSetup? pipelinePreSetup = JsonSerializer.Deserialize<PipelinePreSetup>(pipelinePreSetupValue, JsonExtension.SnakeCaseNamingOption);

        return pipelinePreSetup ?? throw new Exception("NUKE_PRE_SETUP is empty");
    }
}
