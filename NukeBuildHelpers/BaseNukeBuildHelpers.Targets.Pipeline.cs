using Nuke.Common;
using Nuke.Common.ChangeLog;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Entry.Helpers;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.Pipelines.Common;
using NukeBuildHelpers.Pipelines.Common.Models;
using NukeBuildHelpers.RunContext.Interfaces;
using Semver;
using Serilog;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace NukeBuildHelpers;

partial class BaseNukeBuildHelpers
{
    /// <summary>
    /// Target for running entries in the pipeline.
    /// </summary>
    public Target PipelineRunEntry => _ => _
        .Unlisted()
        .Description("To be used by pipeline")
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            ValueHelpers.GetOrFail(() => SplitArgs, out var splitArgs);

            var allEntry = await ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this));

            CheckAppEntry(allEntry);

            List<string> ids = [];
            if (splitArgs.TryGetValue("idsToRun", out var idsToRun))
            {
                ids = [.. idsToRun.NotNullOrWhiteSpace().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
            }
            else
            {
                throw new ArgumentNullException(idsToRun);
            }

            var pipeline = PipelineHelpers.SetupPipeline(this);

            if (splitArgs.TryGetValue("run", out var run))
            {
                switch (run.NotNullOrEmpty().ToLowerInvariant())
                {
                    case "test":
                        await TestAppEntries(allEntry, pipeline, ids, false);
                        break;
                    case "build":
                        await BuildAppEntries(allEntry, pipeline, ids, false);
                        break;
                    case "publish":
                        await PublishAppEntries(allEntry, pipeline, ids, false);
                        break;
                    default:
                        throw new NotImplementedException(splitArgs["run"]);
                }
            }
            else
            {
                throw new ArgumentNullException(run);
            }

        });

    /// <summary>
    /// Target for pre-setup in the pipeline.
    /// </summary>
    public Target PipelinePreSetup => _ => _
        .Unlisted()
        .Description("To be used by pipeline")
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            var allEntry = await ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this));

            CheckAppEntry(allEntry);

            var pipeline = PipelineHelpers.SetupPipeline(this);

            await pipeline.Pipeline.PreparePreSetup(allEntry);

            Log.Information("Target branch: {branch}", pipeline.PipelineInfo.Branch);
            Log.Information("Trigger type: {branch}", pipeline.PipelineInfo.TriggerType);

            EntryHelpers.SetupSecretVariables(this);

            PipelineType = pipeline.PipelineType;

            string env = pipeline.PipelineInfo.Branch.ToLowerInvariant();

            IReadOnlyCollection<Output>? lsRemote = null;

            Dictionary<string, AppRunEntry> toEntry = [];

            long targetBuildId = 0;
            long lastBuildId = 0;

            foreach (var key in allEntry.AppEntryMap.Select(i => i.Key))
            {
                string appId = key;

                ValueHelpers.GetOrFail(appId, allEntry, out var appEntry);
                ValueHelpers.GetOrFail(() => EntryHelpers.GetAllVersions(this, appId, ref lsRemote), out var allVersions);

                if (allVersions.BuildIdCommitPaired.Count > 0)
                {
                    var maxBuildId = allVersions.BuildIdCommitPaired.Select(i => i.Key).Max();
                    lastBuildId = Math.Max(maxBuildId, lastBuildId);
                }

                if (allVersions.EnvSorted.Count == 0 || !allVersions.EnvVersionGrouped.TryGetValue(env, out var versionGroup) || versionGroup.Count == 0)
                {
                    continue;
                }

                if (allVersions.EnvBuildIdGrouped.TryGetValue(env, out var envBuildIdGrouped))
                {
                    foreach (var version in versionGroup.OrderByDescending(i => i))
                    {
                        if (allVersions.VersionPassed.Contains(version) &&
                            allVersions.VersionCommitPaired.TryGetValue(version, out var lastSuccessCommit) &&
                            allVersions.CommitBuildIdGrouped.TryGetValue(lastSuccessCommit, out var buildIdGroup))
                        {
                            var envBuildIdSuccessGrouped = buildIdGroup.Where(envBuildIdGrouped.Contains);
                            targetBuildId = targetBuildId == 0 ? envBuildIdSuccessGrouped.Max() : Math.Min(envBuildIdSuccessGrouped.Max(), targetBuildId);
                            break;
                        }
                    }
                }

                var lastVersionGroup = versionGroup.Last();

                bool hasBumped = false;

                if (!allVersions.EnvLatestVersionPaired.TryGetValue(env, out var currentLatest) || currentLatest != lastVersionGroup)
                {
                    if (allVersions.VersionBump.Contains(lastVersionGroup) &&
                        !allVersions.VersionQueue.Contains(lastVersionGroup) &&
                        !allVersions.VersionFailed.Contains(lastVersionGroup) &&
                        !allVersions.VersionPassed.Contains(lastVersionGroup))
                    {
                        if (pipeline.PipelineInfo.TriggerType == TriggerType.Tag)
                        {
                            hasBumped = true;
                            Log.Information("{appId} Tag: {current}, current latest: {latest}", appId, currentLatest?.ToString(), lastVersionGroup.ToString());
                        }
                    }
                }
                else
                {
                    if (pipeline.PipelineInfo.TriggerType == TriggerType.Tag)
                    {
                        Log.Information("{appId} Tag: {current}, already latest", appId, lastVersionGroup.ToString());
                    }
                }

                toEntry.Add(appId, new()
                {
                    AppId = appEntry.AppId,
                    Environment = env,
                    Version = lastVersionGroup.ToString(),
                    OldVersion = currentLatest?.ToString() ?? "",
                    HasRelease = hasBumped
                });
            }

            foreach (var rel in toEntry.Values.Where(i => i.HasRelease))
            {
                Log.Information("{appId} on {env} has new version {newVersion}", rel.AppId, rel.Environment, rel.Version);
            }

            var releaseNotes = "";
            var buildId = lastBuildId + 1;
            var buildTag = "build." + buildId;
            var targetBuildTag = "build." + targetBuildId;
            var isFirstRelease = targetBuildId == 0;
            var hasEntries = toEntry.Count != 0;
            var hasRelease = toEntry.Any(i => i.Value.HasRelease);

            string versionFactory(string version)
            {
                var semVersion = SemVersion.Parse(version, SemVersionStyles.Strict);
                if (pipeline.PipelineInfo.TriggerType == TriggerType.PullRequest)
                {
                    return SemVersion.Parse($"{semVersion.WithoutMetadata()}+build.{buildId}-pr.{pipeline.PipelineInfo.PullRequestNumber}", SemVersionStyles.Strict).ToString();
                }
                else
                {
                    return SemVersion.Parse($"{semVersion.WithoutMetadata()}+build.{buildId}", SemVersionStyles.Strict).ToString();
                }
            }

            foreach (var entry in toEntry.Values)
            {
                toEntry[entry.AppId] = new()
                {
                    AppId = entry.AppId,
                    Environment = entry.Environment,
                    Version = versionFactory(entry.Version),
                    OldVersion = entry.OldVersion,
                    HasRelease = entry.HasRelease
                };
            }

            if (hasRelease && pipeline.PipelineType != Pipelines.Common.Enums.PipelineType.Local)
            {
                foreach (var entry in toEntry.Values.Where(i => i.HasRelease))
                {
                    var semVersion = SemVersion.Parse(entry.Version, SemVersionStyles.Strict).WithoutMetadata();
                    Git.Invoke("tag -f " + entry.AppId.ToLowerInvariant() + "/" + semVersion + "-queue");
                    Git.Invoke("tag -f " + entry.AppId.ToLowerInvariant() + "/" + semVersion);
                }

                Git.Invoke("tag -f " + buildTag, logger: (s, e) => Log.Debug(e));
                Git.Invoke("tag -f " + buildTag + "-" + env, logger: (s, e) => Log.Debug(e));

                Git.Invoke("push -f --tags", logger: (s, e) => Log.Debug(e));

                string ghReleaseCreateArgs = $"release create {buildTag} " +
                    $"--title {buildTag} " +
                    $"--target {pipeline.PipelineInfo.Branch} " +
                    $"--generate-notes " +
                    $"--draft";

                if (!isFirstRelease)
                {
                    ghReleaseCreateArgs += " --notes-start-tag " + targetBuildTag;
                }

                Gh.Invoke(ghReleaseCreateArgs, logger: (s, e) => Log.Debug(e));

                var releaseNotesJson = Gh.Invoke($"release view {buildTag} --json body", logger: (s, e) => Log.Debug(e)).FirstOrDefault().Text;
                var releaseNotesJsonDocument = JsonSerializer.Deserialize<JsonDocument>(releaseNotesJson);
                if (releaseNotesJsonDocument == null ||
                    !releaseNotesJsonDocument.RootElement.TryGetProperty("body", out var releaseNotesProp) ||
                    releaseNotesProp.GetString() is not string releaseNotesFromProp)
                {
                    throw new Exception("releaseNotesJsonDocument is empty");
                }

                var gitBaseUrl = Repository.HttpsUrl;
                int lastSlashIndex = gitBaseUrl.LastIndexOf('/');
                int lastDotIndex = gitBaseUrl.LastIndexOf('.');
                if (lastDotIndex > lastSlashIndex)
                {
                    gitBaseUrl = gitBaseUrl[..lastDotIndex];
                }

                var newVersionsMarkdown = "## New Versions";
                foreach (var entry in toEntry.Values.Where(i => i.HasRelease))
                {
                    var appId = entry.AppId.ToLowerInvariant();
                    var oldVer = entry.OldVersion;
                    var newVer = SemVersion.Parse(entry.Version, SemVersionStyles.Strict).WithoutMetadata().ToString();
                    if (string.IsNullOrEmpty(oldVer))
                    {
                        newVersionsMarkdown += $"\n* Bump `{appId}` to `{newVer}`. See [changelog]({gitBaseUrl}/commits/{appId}/{newVer})";
                    }
                    else
                    {
                        newVersionsMarkdown += $"\n* Bump `{appId}` from `{oldVer}` to `{newVer}`. See [changelog]({gitBaseUrl}/compare/{appId}/{oldVer}...{appId}/{newVer})";
                    }
                }

                if (releaseNotesFromProp.Contains("\n\n\n**Full Changelog**"))
                {
                    releaseNotes = releaseNotesFromProp.Replace("\n\n\n**Full Changelog**", "\n\n" + newVersionsMarkdown + "\n\n\n**Full Changelog**");
                }
                else if (releaseNotesFromProp.Contains("**Full Changelog**"))
                {
                    releaseNotes = releaseNotesFromProp.Replace("**Full Changelog**", newVersionsMarkdown + "\n\n\n**Full Changelog**");
                }
                else
                {
                    releaseNotes = newVersionsMarkdown;
                }

                var notesPath = TemporaryDirectory / "notes.md";
                notesPath.WriteAllText(releaseNotes);

                string ghReleaseEditArgs = $"release edit {buildTag} " +
                    $"--notes-file {notesPath}";

                Gh.Invoke(ghReleaseEditArgs, logger: (s, e) => Log.Debug(e));
            }

            foreach (var targetEntry in allEntry.TargetEntryDefinitionMap.Values)
            {
                if (!toEntry.TryGetValue(ValueHelpers.GetOrNullFail(targetEntry.AppId), out var entry))
                {
                    continue;
                }

                RunType runType = pipeline.PipelineInfo.TriggerType switch
                {
                    TriggerType.PullRequest => RunType.PullRequest,
                    TriggerType.Commit => RunType.Commit,
                    TriggerType.Tag => entry.HasRelease ? RunType.Bump : RunType.Commit,
                    TriggerType.Local => RunType.Local,
                    _ => throw new NotSupportedException()
                };

                AppVersion appVersion = new()
                {
                    AppId = entry.AppId.NotNullOrEmpty(),
                    Environment = entry.Environment,
                    Version = SemVersion.Parse(entry.Version, SemVersionStyles.Strict),
                    BuildId = buildId
                };

                SetupTargetRunContext(targetEntry, runType, appVersion, releaseNotes, pipeline);
            }

            Dictionary<string, EntrySetup> testEntrySetupMap = [];
            Dictionary<string, EntrySetup> buildEntrySetupMap = [];
            Dictionary<string, EntrySetup> publishEntrySetupMap = [];
            Dictionary<string, EntrySetup> targetEntrySetupMap = [];
            Dictionary<string, EntrySetup> dependentEntrySetupMap = [];
            Dictionary<string, EntrySetup> entrySetupMap = [];

            async Task<EntrySetup> createEntrySetup(IRunEntryDefinition entry)
            {
                var runnerOs = await entry.GetRunnerOS();
                var cachePaths = await entry.GetCachePaths();
                var flatCachePaths = cachePaths.Select(i =>
                {
                    string flatPath = i.ToString();
                    if (!flatPath.StartsWith(RootDirectory))
                    {
                        throw new Exception("Invalid cache path");
                    }
                    return flatPath[RootDirectory.ToString().Length..];
                });

                EntrySetup setup = new()
                {
                    Id = entry.Id,
                    RunType = ValueHelpers.GetOrNullFail(entry.RunContext).RunType,
                    Condition = await entry.GetCondition(),
                    RunnerOSSetup = new()
                    {
                        Name = runnerOs.Name,
                        RunnerPipelineOS = JsonSerializer.Serialize(runnerOs.GetPipelineOS(PipelineType), JsonExtension.SnakeCaseNamingOptionIndented),
                        RunScript = runnerOs.GetRunScript(PipelineType)
                    },
                    CacheInvalidator = await entry.GetCacheInvalidator(),
                    CachePaths = flatCachePaths.ToArray(),
                    CheckoutFetchDepth = await entry.GetCheckoutFetchDepth(),
                    CheckoutFetchTags = await entry.GetCheckoutFetchTags(),
                    CheckoutSubmodules = await entry.GetCheckoutSubmodules()
                };

                return setup;
            }

            foreach (var entry in allEntry.PublishEntryDefinitionMap.Values)
            {
                EntrySetup setup = await createEntrySetup(entry);
                publishEntrySetupMap.Add(entry.Id, setup);
                targetEntrySetupMap.Add(entry.Id, setup);
                entrySetupMap.Add(entry.Id, setup);
            }

            foreach (var entry in allEntry.BuildEntryDefinitionMap.Values)
            {
                EntrySetup setup = await createEntrySetup(entry);
                buildEntrySetupMap.Add(entry.Id, setup);
                targetEntrySetupMap.Add(entry.Id, setup);
                entrySetupMap.Add(entry.Id, setup);
            }

            foreach (var dependentEntry in allEntry.DependentEntryDefinitionMap.Values)
            {
                List<(EntrySetup Setup, IBuildEntryDefinition Entry)> buildEntries = [];
                List<(EntrySetup Setup, IPublishEntryDefinition Entry)> publishEntries = [];
                List<(EntrySetup Setup, ITargetEntryDefinition Entry)> targetEntries = [];

                foreach (var appId in dependentEntry.AppIds)
                {
                    foreach (var entrySetup in entrySetupMap.Values)
                    {
                        if (allEntry.TargetEntryDefinitionMap.TryGetValue(entrySetup.Id, out var targetEntry) &&
                            ValueHelpers.GetOrNullFail(targetEntry.AppId).Equals(appId, StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (targetEntry is IBuildEntryDefinition buildEntry)
                            {
                                buildEntries.Add((entrySetup, buildEntry));
                            }
                            else if (targetEntry is IPublishEntryDefinition publishEntry)
                            {
                                publishEntries.Add((entrySetup, publishEntry));
                            }
                        }
                    }
                }

                RunType runType = pipeline.PipelineInfo.TriggerType switch
                {
                    TriggerType.PullRequest => RunType.PullRequest,
                    TriggerType.Commit => RunType.Commit,
                    TriggerType.Tag => targetEntries.Any(i => i.Entry.RunContext is IBumpContext) ? RunType.Bump : RunType.Commit,
                    TriggerType.Local => RunType.Local,
                    _ => throw new NotSupportedException()
                };

                if (targetEntries.Any(i => i.Setup.Condition))
                {
                    runType |= RunType.Target;
                }

                SetupDependentRunContext(dependentEntry, runType);
            }

            foreach (var entry in allEntry.DependentEntryDefinitionMap.Values)
            {
                EntrySetup setup = await createEntrySetup(entry);
                testEntrySetupMap.Add(entry.Id, setup);
                dependentEntrySetupMap.Add(entry.Id, setup);
                entrySetupMap.Add(entry.Id, setup);
            }

            PipelinePreSetup pipelinePreSetup = new()
            {
                Branch = pipeline.PipelineInfo.Branch,
                TriggerType = pipeline.PipelineInfo.TriggerType,
                Environment = env,
                ReleaseNotes = releaseNotes,
                BuildId = buildId,
                LastBuildId = targetBuildId,
                PullRequestNumber = pipeline.PipelineInfo.PullRequestNumber,
                HasRelease = hasRelease,
                TestEntries = [.. testEntrySetupMap.Keys],
                BuildEntries = [.. buildEntrySetupMap.Keys],
                PublishEntries = [.. publishEntrySetupMap.Keys],
                EntrySetupMap = entrySetupMap,
                AppRunEntryMap = toEntry
            };

            await pipeline.Pipeline.FinalizePreSetup(allEntry, pipelinePreSetup);
        });

    /// <summary>
    /// Target for post-setup in the pipeline.
    /// </summary>
    public Target PipelinePostSetup => _ => _
        .Unlisted()
        .Description("To be used by pipeline")
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            ValueHelpers.GetOrFail(() => SplitArgs, out var splitArgs);

            var allEntry = await ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this));

            CheckAppEntry(allEntry);

            var pipeline = PipelineHelpers.SetupPipeline(this);

            var pipelinePreSetup = pipeline.Pipeline.GetPipelinePreSetup();

            await pipeline.Pipeline.PreparePostSetup(allEntry, pipelinePreSetup);

            bool success = true;

            foreach (var entryDefinition in allEntry.RunEntryDefinitionMap.Values)
            {
                var entryRunResult = Environment.GetEnvironmentVariable("NUKE_RUN_RESULT_" + entryDefinition.Id.ToUpperInvariant());
                Log.Information("{entryId} result: {result}", entryDefinition.Id, entryRunResult);
                if (entryRunResult == "error" && success)
                {
                    success = false;
                }
            }

            if (pipelinePreSetup != null)
            {
                if (success)
                {
                    if (pipelinePreSetup.HasRelease)
                    {
                        var assetOutput = TemporaryDirectory / "assets";

                        assetOutput.CreateOrCleanDirectory();

                        foreach (var artifact in CommonArtifactsDirectory.GetFiles())
                        {
                            if (!artifact.HasExtension(".zip"))
                            {
                                continue;
                            }
                            var appId = artifact.Name.Split(artifactNameSeparator).FirstOrDefault().NotNullOrEmpty().ToLowerInvariant();
                            artifact.UnZipTo(OutputDirectory / appId);
                        }

                        foreach (var appRunEntry in pipelinePreSetup.AppRunEntryMap.Values.Where(i => i.HasRelease))
                        {
                            if (!allEntry.AppEntryMap.TryGetValue(appRunEntry.AppId, out var appEntry))
                            {
                                continue;
                            }
                            var appIdLower = appEntry.AppId.ToLowerInvariant();
                            var releasePath = OutputDirectory / appIdLower;
                            if (!releasePath.DirectoryExists())
                            {
                                throw new Exception("No release found for " + appIdLower);
                            }
                            var commonAssetPath = releasePath / "common_asset";
                            if (commonAssetPath.DirectoryExists() && (commonAssetPath.GetDirectories().Any() || commonAssetPath.GetFiles().Any()))
                            {
                                var commonOutPath = TemporaryDirectory / "archive" / appIdLower + "-" + appRunEntry.Version;
                                await commonAssetPath.CopyRecursively(commonOutPath);
                                commonOutPath.ZipTo(assetOutput / commonOutPath.Name + ".zip");
                                Log.Information("Publish common asset {appId}: {name}", appIdLower, commonOutPath.Name + ".zip");
                            }
                            var individualAssetPath = releasePath / "asset";
                            if (individualAssetPath.DirectoryExists() && individualAssetPath.GetFiles().Any())
                            {
                                foreach (var releaseAsset in individualAssetPath.GetFiles())
                                {
                                    await releaseAsset.CopyRecursively(assetOutput / releaseAsset.Name);
                                    Log.Information("Publish individual asset {appId}: {name}", appIdLower, releaseAsset.Name);
                                }
                            }
                        }

                        await Task.Run(() =>
                        {
                            foreach (var appRunEntry in pipelinePreSetup.AppRunEntryMap.Values.Where(i => i.HasRelease))
                            {
                                if (!allEntry.AppEntryMap.TryGetValue(appRunEntry.AppId, out var appEntry))
                                {
                                    continue;
                                }
                                var appIdLower = appEntry.AppId.ToLowerInvariant();
                                var version = SemVersion.Parse(appRunEntry.Version, SemVersionStyles.Strict).WithoutMetadata();
                                string latestTag = "latest";
                                if (!appRunEntry.Environment.Equals(MainEnvironmentBranch, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    latestTag += "-" + appRunEntry.Environment;
                                }

                                Git.Invoke("tag -f " + appIdLower + "/" + version + "-passed");
                                Git.Invoke("tag -f " + appIdLower + "/" + version);
                                Git.Invoke("tag -f " + appIdLower + "/" + latestTag);
                            }

                            Git.Invoke("push -f --tags", logger: (s, e) => Log.Debug(e));

                            var assetReleaseFiles = assetOutput.GetFiles("*.*");
                            if (assetReleaseFiles.Any())
                            {
                                Gh.Invoke("release upload --clobber build." + pipelinePreSetup.BuildId + " " + string.Join(" ", assetReleaseFiles.Select(i => i.ToString())));
                                
                                var releaseNotesJson = Gh.Invoke($"release view build.{pipelinePreSetup.BuildId} --json body", logger: (s, e) => Log.Debug(e)).FirstOrDefault().Text;
                                var releaseNotesJsonDocument = JsonSerializer.Deserialize<JsonDocument>(releaseNotesJson);
                                if (releaseNotesJsonDocument == null ||
                                    !releaseNotesJsonDocument.RootElement.TryGetProperty("body", out var releaseNotesProp) ||
                                    releaseNotesProp.GetString() is not string releaseNotes)
                                {
                                    throw new Exception("releaseNotesJsonDocument is empty");
                                }

                                releaseNotes += "\n\n---\n\n## Asset Hashes\n| Asset | Hashes |\n|---|---|\n";
                                releaseNotes += "\n";
                                foreach (var assetFile in assetReleaseFiles)
                                {
                                    releaseNotes += $"| **{assetFile.Name}** | <details><summary>Click to expand</summary> ";
                                    foreach (var fileHash in FileHashesToCreate)
                                    {
                                        using var stream = File.OpenRead(assetFile);
                                        byte[] hashBytes = fileHash.HashAlgorithm.ComputeHash(stream);
                                        var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                                        releaseNotes += $"**{fileHash.Name}:** `{hash}`<br> ";
                                    }
                                    releaseNotes += "</details> |\n";
                                }

                                var notesPath = TemporaryDirectory / "notes.md";
                                notesPath.WriteAllText(releaseNotes);

                                Gh.Invoke($"release edit --notes-file={notesPath} build.{pipelinePreSetup.BuildId}");
                            }

                            Gh.Invoke($"release edit --draft=false build.{pipelinePreSetup.BuildId}");
                        });
                    }
                }
                else
                {
                    if (pipelinePreSetup.HasRelease)
                    {
                        await Task.Run(() =>
                        {
                            foreach (var appRunEntry in pipelinePreSetup.AppRunEntryMap.Values.Where(i => i.HasRelease))
                            {
                                var version = SemVersion.Parse(appRunEntry.Version, SemVersionStyles.Strict).WithoutMetadata();
                                string latestTag = "latest";
                                if (!appRunEntry.Environment.Equals(MainEnvironmentBranch, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    latestTag += "-" + appRunEntry.Environment;
                                }

                                Git.Invoke("tag -f " + appRunEntry.AppId + "/" + version + "-failed");
                                Git.Invoke("tag -f " + appRunEntry.AppId + "/" + version);
                            }

                            Gh.Invoke("release delete -y build." + pipelinePreSetup.BuildId);

                            Git.Invoke("push -f --tags", logger: (s, e) => Log.Debug(e));
                        });
                    }
                }
            }

            await pipeline.Pipeline.FinalizePostSetup(allEntry, pipelinePreSetup);

            if (!success)
            {
                throw new Exception("Run has error(s)");
            }
        });
}
