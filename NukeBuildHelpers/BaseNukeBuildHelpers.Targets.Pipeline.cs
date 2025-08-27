using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Git;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Common.Models;
using NukeBuildHelpers.Entry.Helpers;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.Pipelines.Common;
using NukeBuildHelpers.Pipelines.Common.Models;
using NukeBuildHelpers.RunContext.Interfaces;
using Semver;
using Serilog;
using System.Text.Json;

namespace NukeBuildHelpers;

partial class BaseNukeBuildHelpers
{
    /// <summary>
    /// Target for pre-setup in the pipeline.
    /// </summary>
    public Target PipelinePreSetup => _ => _
        .Unlisted()
        .Description("To be used by pipeline")
        .Executes(async () =>
        {
            await RunPipelinePreSetup();
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

            EntryHelpers.SetupSecretVariables(this);

            var pipeline = await PipelineHelpers.SetupPipeline(this);

            var pipelinePreSetup = await pipeline.Pipeline.GetPipelinePreSetup();

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

            if (success)
            {
                if (pipelinePreSetup.HasRelease)
                {
                    var assetOutput = TemporaryDirectory / "release_assets";

                    assetOutput.CreateOrCleanDirectory();

                    foreach (var artifact in CommonArtifactsDirectory.GetFiles())
                    {
                        if (!artifact.HasExtension(".zip"))
                        {
                            continue;
                        }
                        var appId = artifact.Name.Split(ArtifactNameSeparator).Skip(1).FirstOrDefault().NotNullOrEmpty().ToLowerInvariant();
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
                        var commonAssetPath = releasePath / "common_assets";
                        if (commonAssetPath.DirectoryExists() && (commonAssetPath.GetDirectories().Any() || commonAssetPath.GetFiles().Any()))
                        {
                            var commonOutPath = TemporaryDirectory / "archive" / appIdLower + "-" + appRunEntry.Version;
                            await commonAssetPath.CopyTo(commonOutPath);
                            commonOutPath.ZipTo(assetOutput / commonOutPath.Name + ".zip");
                            Log.Information("Publish common asset {appId}: {name}", appIdLower, commonOutPath.Name + ".zip");
                        }
                        var individualAssetPath = releasePath / "assets";
                        if (individualAssetPath.DirectoryExists() && individualAssetPath.GetFiles().Any())
                        {
                            foreach (var releaseAsset in individualAssetPath.GetFiles())
                            {
                                await releaseAsset.CopyTo(assetOutput / releaseAsset.Name);
                                Log.Information("Publish individual asset {appId}: {name}", appIdLower, releaseAsset.Name);
                            }
                        }
                    }

                    var repoViewJson = Gh.Invoke($"repo view --json url", logOutput: false, logInvocation: false).FirstOrDefault().Text;
                    var repoViewJsonDocument = JsonSerializer.Deserialize<JsonDocument>(repoViewJson);
                    if (repoViewJsonDocument == null ||
                        !repoViewJsonDocument.RootElement.TryGetProperty("url", out var baseUrlJsonProp) ||
                        baseUrlJsonProp.GetString() is not string baseUri)
                    {
                        throw new Exception("repoViewJson is invalid");
                    }

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

                    var releaseJson = Gh.Invoke($"release view build.{pipelinePreSetup.BuildId} --json body", logOutput: false, logInvocation: false).FirstOrDefault().Text;
                    var releaseJsonDocument = JsonSerializer.Deserialize<JsonDocument>(releaseJson);
                    if (releaseJsonDocument == null ||
                        !releaseJsonDocument.RootElement.TryGetProperty("body", out var releaseNotesProp) ||
                        releaseNotesProp.GetString() is not string releaseNotes)
                    {
                        throw new Exception("releaseJsonDocument is invalid");
                    }

                    var assetReleaseFiles = assetOutput.GetFiles("*.*");
                    if (assetReleaseFiles.Any())
                    {
                        Gh.Invoke("release upload --clobber build." + pipelinePreSetup.BuildId + " " + string.Join(" ", assetReleaseFiles.Select(i => i.ToString())));

                        if (await allEntry.WorkflowConfigEntryDefinition.GetAppendReleaseNotesAssetHashes())
                        {
                            if (releaseNotes.LastOrDefault() != '\n')
                            {
                                releaseNotes += "\n";
                            }
                            releaseNotes += "\n---\n\n## Asset Hashes\n| Asset | Hashes |\n|---|---|\n";

                            foreach (var assetFile in assetReleaseFiles)
                            {
                                var url = new Uri(baseUri.Trim('/') + $"/releases/download/build.{pipelinePreSetup.BuildId}/{assetFile.Name}");
                                releaseNotes += $"| **[{assetFile.Name}]({url})** | <details><summary>Click to expand</summary> ";
                                foreach (var (HashAlgorithm, Name) in FileHashesToCreate)
                                {
                                    var hash = await assetFile.GetHash(HashAlgorithm);
                                    releaseNotes += $"**{Name}:** `{hash}`<br> ";
                                }
                                releaseNotes += "</details> |\n";
                            }

                            var notesPath = TemporaryDirectory / "notes.md";
                            notesPath.WriteAllText(releaseNotes);

                            Gh.Invoke($"release edit --notes-file={notesPath} build.{pipelinePreSetup.BuildId}");
                        }
                    }

                    Log.Information("Final release notes:\n{Notes}", releaseNotes);

                    Gh.Invoke($"release edit --draft=false build.{pipelinePreSetup.BuildId}");
                }
            }
            else
            {
                if (pipelinePreSetup.HasRelease)
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
                }
            }

            await pipeline.Pipeline.FinalizePostSetup(allEntry, pipelinePreSetup);

            if (!success)
            {
                throw new Exception("Run has error(s)");
            }
        });
}
