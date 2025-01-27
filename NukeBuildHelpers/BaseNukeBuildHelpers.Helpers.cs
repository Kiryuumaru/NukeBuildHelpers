using Nuke.Common;
using Nuke.Common.IO;
using Semver;
using Serilog;
using NukeBuildHelpers.Common;
using Nuke.Common.Tooling;
using Nuke.Common.Utilities;
using Sharprompt;
using System.ComponentModel.DataAnnotations;
using NukeBuildHelpers.ConsoleInterface;
using NukeBuildHelpers.ConsoleInterface.Models;
using System.Text.Json;
using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.ConsoleInterface.Enums;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.Pipelines.Common.Models;
using NukeBuildHelpers.Entry.Helpers;
using System.Security.Cryptography;
using NukeBuildHelpers.RunContext.Models;
using NukeBuildHelpers.Common.Models;
using NukeBuildHelpers.Entry.Definitions;
using NukeBuildHelpers.Pipelines.Common;
using NukeBuildHelpers.Pipelines.Github;
using NukeBuildHelpers.Pipelines.Azure;
using System.Reflection;
using Nuke.Common.Utilities.Collections;

namespace NukeBuildHelpers;

partial class BaseNukeBuildHelpers
{
    internal const string ArtifactNameSeparator = "___";

    private static readonly AbsolutePath entryCachePath = CommonCacheDirectory / "entry";
    private static readonly AbsolutePath entryCacheIndexPath = CommonCacheDirectory / "entry_index";
    private static readonly List<(HashAlgorithm HashAlgorithm, string Name)> FileHashesToCreate =
    [
        (MD5.Create(), "MD5"),
        (SHA1.Create(), "SHA-1"),
        (SHA256.Create(), "SHA-256"),
        (SHA512.Create(), "SHA-512")
    ];

    private void CheckEnvironementBranches()
    {
        HashSet<string> set = [];

        foreach (string env in EnvironmentBranches.Select(i => i.ToLowerInvariant()))
        {
            if (!set.Add(env))
            {
                throw new Exception($"Duplicate environment branch \"{env}\"");
            }
        }

        if (!set.Contains(MainEnvironmentBranch.ToLowerInvariant()))
        {
            throw new Exception($"MainEnvironmentBranch \"{MainEnvironmentBranch}\" does not exists in EnvironmentBranches");
        }
    }

    private static void CheckAppEntry(AllEntry allEntry)
    {
        if (allEntry.AppEntryMap.Count == 0)
        {
            throw new Exception($"No configured app entry.");
        }
    }

    private static void CacheBump()
    {
        if (!CommonCacheOutputDirectory.DirectoryExists())
        {
            CommonCacheOutputDirectory.CreateDirectory();
        }

        (CommonCacheDirectory / "stamp").WriteAllText(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
    }

    private static void OutputBump()
    {
        if (!CommonOutputDirectory.DirectoryExists())
        {
            CommonOutputDirectory.CreateDirectory();
        }

        (CommonOutputDirectory / "stamp").WriteAllText(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
    }

    private static Dictionary<string, AbsolutePath> GetCacheIndex()
    {
        Dictionary<string, AbsolutePath> cachePairs = [];

        if (entryCacheIndexPath.FileExists())
        {
            try
            {
                var cachePairsStr = JsonSerializer.Deserialize<Dictionary<string, string>>(entryCacheIndexPath.ReadAllText()) ?? [];
                foreach (var pair in cachePairsStr)
                {
                    try
                    {
                        cachePairs[pair.Key] = pair.Value;
                    }
                    catch { }
                }
            }
            catch { }
        }

        return cachePairs;
    }

    private static void SetCacheIndex(Dictionary<string, AbsolutePath> cacheIndex)
    {
        try
        {
            if (!entryCacheIndexPath.Parent.DirectoryExists())
            {
                entryCacheIndexPath.Parent.CreateDirectory();
            }
            entryCacheIndexPath.WriteAllText(JsonSerializer.Serialize(cacheIndex.ToDictionary(i => i.Key, i => i.Value.ToString())));
        }
        catch { }
    }

    private static async Task CachePreload(IRunEntryDefinition entry)
    {
        if (!entryCachePath.DirectoryExists())
        {
            entryCachePath.CreateDirectory();
        }

        var cachePaths = entry.CachePath == null ? [] : await entry.GetCachePaths();

        Dictionary<string, AbsolutePath> cachePairs = GetCacheIndex();

        List<Task> tasks = [];

        foreach (var dir in entryCachePath.GetDirectories())
        {
            if (!cachePairs.Any(i => i.Value == dir))
            {
                dir.DeleteDirectory();
                Log.Information("{Path} cache cleaned", dir);
            }
        }

        foreach (var pair in cachePairs.Clone())
        {
            if (!cachePaths.Any(i => i == pair.Key))
            {
                cachePairs.Remove(pair.Key);
                pair.Value.DeleteDirectory();
                Log.Information("{Path} cache cleaned", pair.Key);
            }
        }

        foreach (var path in cachePaths)
        {
            if (!cachePairs.TryGetValue(path.ToString(), out var cachePath) || !cachePath.DirectoryExists())
            {
                Log.Information("{Path} cache missed", path);
                continue;
            }
            tasks.Add(Task.Run(async () =>
            {
                var cachePathValue = cachePath / "currentLatest";
                path.Parent.CreateDirectory();
                if (cachePathValue.FileExists())
                {
                    File.Move(cachePathValue, path, true);
                }
                else if (cachePathValue.DirectoryExists())
                {
                    await cachePathValue.MoveTo(path);
                }
                Log.Information("{Path} cache loaded", path);
            }));
        }

        await Task.WhenAll(tasks);

        SetCacheIndex(cachePairs);
    }

    private static async Task CachePostload(IRunEntryDefinition entry)
    {
        var cachePaths = entry.CachePath == null ? [] : await entry.GetCachePaths();

        Dictionary<string, AbsolutePath> cachePairs = GetCacheIndex();

        List<Task> tasks = [];

        foreach (var path in cachePaths)
        {
            if (!path.FileExists() && !path.DirectoryExists())
            {
                Log.Information("{Path} cache missed", path);
                continue;
            }
            if (!cachePairs.TryGetValue(path.ToString(), out var cachePath))
            {
                cachePath = entryCachePath / Guid.NewGuid().Encode();
                cachePairs[path.ToString()] = cachePath;
            }
            tasks.Add(Task.Run(async () =>
            {
                var cachePathValue = cachePath / "currentLatest";
                cachePath.CreateDirectory();
                if (path.FileExists())
                {
                    File.Move(path, cachePathValue, true);
                }
                else if (path.DirectoryExists())
                {
                    await path.MoveTo(cachePathValue);
                }
                Log.Information("{Path} cache saved", path);
            }));
        }

        await Task.WhenAll(tasks);

        SetCacheIndex(cachePairs);
    }

    private void SetupTargetRunContext(ITargetEntryDefinition targetEntry, RunType runType, AppVersion? appVersion, string releaseNotes, PipelineRun pipeline)
    {
        if (appVersion == null || runType == RunType.Local)
        {
            targetEntry.RunContext = new LocalContext()
            {
                RunType = RunType.Local
            };
        }
        else if (runType == RunType.Bump)
        {
            targetEntry.RunContext = new BumpContext()
            {
                PipelineType = PipelineType,
                RunType = runType,
                AppVersion = new BumpReleaseVersion()
                {
                    AppId = appVersion.AppId,
                    Environment = appVersion.Environment,
                    Version = appVersion.Version,
                    BuildId = appVersion.BuildId,
                    ReleaseNotes = releaseNotes
                }
            };
        }
        else if (runType == RunType.PullRequest)
        {
            targetEntry.RunContext = new PullRequestContext()
            {
                PipelineType = PipelineType,
                RunType = runType,
                AppVersion = new PullRequestReleaseVersion()
                {
                    AppId = appVersion.AppId,
                    Environment = appVersion.Environment,
                    Version = appVersion.Version,
                    BuildId = appVersion.BuildId,
                    PullRequestNumber = pipeline.PipelineInfo.PullRequestNumber
                }
            };
        }
        else
        {
            targetEntry.RunContext = new VersionedContext()
            {
                PipelineType = PipelineType,
                RunType = runType,
                AppVersion = appVersion
            };
        }
    }

    private void SetupDependentRunContext(IDependentEntryDefinition dependentEntry, RunType runType)
    {
        if (runType == RunType.Local)
        {
            dependentEntry.RunContext = new LocalContext()
            {
                RunType = RunType.Local
            };
        }
        else
        {
            dependentEntry.RunContext = new CommitContext()
            {
                PipelineType = PipelineType,
                RunType = runType
            };
        }
    }

    private async Task EntryPreSetup(AllEntry allEntry, PipelineRun pipeline, PipelinePreSetup? pipelinePreSetup)
    {
        EntryHelpers.SetupSecretVariables(this);

        PipelineType = pipeline.PipelineType;

        string releaseNotes = "";

        if (pipelinePreSetup != null &&
            pipelinePreSetup.HasRelease &&
            pipeline.PipelineType != Pipelines.Common.Enums.PipelineType.Local)
        {
            await Task.Run(() =>
            {
                var releaseNotesJson = Gh.Invoke($"release view build.{pipelinePreSetup.BuildId} --json body", logOutput: false, logInvocation: false).FirstOrDefault().Text;
                var releaseNotesJsonDocument = JsonSerializer.Deserialize<JsonDocument>(releaseNotesJson);
                if (releaseNotesJsonDocument == null ||
                    !releaseNotesJsonDocument.RootElement.TryGetProperty("body", out var releaseNotesProp) ||
                    releaseNotesProp.GetString() is not string releaseNotesFromProp)
                {
                    throw new Exception("releaseNotesJsonDocument is empty");
                }
                releaseNotes = releaseNotesFromProp;
            });
        }

        foreach (var targetEntry in allEntry.TargetEntryDefinitionMap.Values)
        {
            if (pipelinePreSetup == null)
            {
                SetupTargetRunContext(targetEntry, RunType.Local, null, "", pipeline);
                continue;
            }

            if (!pipelinePreSetup.AppRunEntryMap.TryGetValue(ValueHelpers.GetOrNullFail(targetEntry.AppId), out var entry))
            {
                throw new Exception("App id not found");
            }
            if (!pipelinePreSetup.EntrySetupMap.TryGetValue(ValueHelpers.GetOrNullFail(targetEntry.Id), out var entrySetup))
            {
                throw new Exception("Entry id not found");
            }

            SemVersion version;
            if (SemVersion.TryParse(entry.Version, SemVersionStyles.Strict, out var parsedSemVersion))
            {
                version = parsedSemVersion;
            }
            else if (SemVersion.TryParse($"0.0.0-{entry.Environment}.0", SemVersionStyles.Strict, out var parsedMinSemVersion))
            {
                version = parsedMinSemVersion;
            }
            else
            {
                version = SemVersion.Parse($"0.0.0", SemVersionStyles.Strict);
            }

            AppVersion appVersion = new()
            {
                AppId = entry.AppId.NotNullOrEmpty(),
                Environment = entry.Environment,
                Version = version,
                BuildId = pipelinePreSetup.BuildId
            };

            SetupTargetRunContext(targetEntry, entrySetup.RunType, appVersion, releaseNotes, pipeline);
        }

        foreach (var dependentEntry in allEntry.DependentEntryDefinitionMap.Values)
        {
            if (pipelinePreSetup == null)
            {
                SetupDependentRunContext(dependentEntry, RunType.Local);
                continue;
            }

            if (!pipelinePreSetup.EntrySetupMap.TryGetValue(ValueHelpers.GetOrNullFail(dependentEntry.Id), out var entrySetup))
            {
                throw new Exception("Entry id not found");
            }

            SetupDependentRunContext(dependentEntry, entrySetup.RunType);
        }
    }

    private async Task RunEntry(AllEntry allEntry, PipelineRun pipeline, IEnumerable<IRunEntryDefinition> entriesToRun, PipelinePreSetup? pipelinePreSetup, Func<IRunEntryDefinition, Task>? preExecute, Func<IRunEntryDefinition, Task>? postExecute)
    {
        bool skipCache = pipeline.PipelineInfo.TriggerType == TriggerType.Local || pipelinePreSetup == null;

        await pipeline.Pipeline.PrepareEntryRun(allEntry, pipelinePreSetup, entriesToRun.ToDictionary(i => i.Id));

        CacheBump();

        await EntryPreSetup(allEntry, pipeline, pipelinePreSetup);

        foreach (var entry in entriesToRun)
        {
            Console.WriteLine();
            Console.WriteLine($"""
                ═══════════════════════════════════════
                {entry.Id}
                ───────────────────────────────────────
                """);
            Console.WriteLine();

            OutputDirectory.CreateOrCleanDirectory();

            if (!skipCache)
            {
                await CachePreload(entry);
            }

            OutputBump();

            if (preExecute != null)
            {
                await preExecute.Invoke(entry);
            }

            await entry.GetExecute();

            if (postExecute != null)
            {
                await postExecute.Invoke(entry);
            }
            if (!skipCache)
            {
                await CachePostload(entry);
            }
        }

        await pipeline.Pipeline.FinalizeEntryRun(allEntry, pipelinePreSetup, entriesToRun.ToDictionary(i => i.Id));
    }

    private Task RunEntry(AllEntry allEntry, PipelineRun pipeline, IEnumerable<IRunEntryDefinition> entriesToRun, PipelinePreSetup? pipelinePreSetup)
    {
        return RunEntry(allEntry, pipeline, entriesToRun, pipelinePreSetup, entry =>
        {
            switch (entry)
            {
                case IBuildEntryDefinition buildEntryDefinition:
                    break;
                case ITestEntryDefinition testEntryDefinition:
                    if (CommonArtifactsDirectory.DirectoryExists())
                    {
                        foreach (var artifact in CommonArtifactsDirectory.GetFiles())
                        {
                            if (!artifact.HasExtension(".zip"))
                            {
                                continue;
                            }
                            var appId = artifact.Name.Split(ArtifactNameSeparator).Skip(1).FirstOrDefault().NotNullOrEmpty().ToLowerInvariant();
                            if (testEntryDefinition.AppIds.Any(i => i.Equals(appId, StringComparison.InvariantCultureIgnoreCase)))
                            {
                                artifact.UnZipTo(CommonOutputDirectory);
                            }
                        }
                    }
                    break;
                case IPublishEntryDefinition publishEntryDefinition:
                    if (CommonArtifactsDirectory.DirectoryExists())
                    {
                        foreach (var artifact in CommonArtifactsDirectory.GetFiles())
                        {
                            if (!artifact.HasExtension(".zip"))
                            {
                                continue;
                            }
                            var appId = artifact.Name.Split(ArtifactNameSeparator).Skip(1).FirstOrDefault().NotNullOrEmpty().ToLowerInvariant();
                            if (appId.Equals(publishEntryDefinition.AppId, StringComparison.InvariantCultureIgnoreCase))
                            {
                                artifact.UnZipTo(CommonOutputDirectory);
                            }
                        }
                    }
                    break;
            }
            return Task.CompletedTask;

        }, async entry =>
        {
            switch (entry)
            {
                case IBuildEntryDefinition buildEntryDefinition:
                    var buildArtifactName = "build" + ArtifactNameSeparator + buildEntryDefinition.AppId.NotNullOrEmpty().ToLowerInvariant() + ArtifactNameSeparator + buildEntryDefinition.Id.ToUpperInvariant();
                    var buildArtifactTempPath = TemporaryDirectory / buildArtifactName;
                    var buildArtifactFilePath = CommonArtifactsUploadDirectory / $"{buildArtifactName}.zip";
                    buildArtifactTempPath.CreateOrCleanDirectory();
                    buildArtifactFilePath.DeleteFile();
                    await CommonOutputDirectory.MoveTo(buildArtifactTempPath);
                    buildArtifactTempPath.ZipTo(buildArtifactFilePath);
                    break;
                case ITestEntryDefinition testEntryDefinition:
                    break;
                case IPublishEntryDefinition publishEntryDefinition:
                    var releaseAssetsDir = TemporaryDirectory / "release_assets";
                    //var releaseAssetsDir = CommonOutputDirectory;
                    var assetOutDir = releaseAssetsDir / "assets";
                    var commonAssetOutDir = releaseAssetsDir / "common_assets";
                    assetOutDir.CreateOrCleanDirectory();
                    commonAssetOutDir.CreateOrCleanDirectory();
                    foreach (var asset in await publishEntryDefinition.GetReleaseAssets())
                    {
                        if (asset.FileExists())
                        {
                            await asset.CopyTo(assetOutDir / asset.Name);
                        }
                        else if (asset.DirectoryExists())
                        {
                            var destinationPath = assetOutDir / (asset.Name + ".zip");
                            if (destinationPath.FileExists())
                            {
                                destinationPath.DeleteFile();
                            }
                            asset.ZipTo(destinationPath);
                        }
                        Log.Information("Added {file} to release assets", asset);
                    }
                    foreach (var asset in await publishEntryDefinition.GetReleaseCommonAssets())
                    {
                        if (asset.FileExists() || asset.DirectoryExists())
                        {
                            await asset.CopyTo(commonAssetOutDir);
                            Log.Information("Added {file} to common assets", asset);
                        }
                    }
                    var publishArtifactName = "publish" + ArtifactNameSeparator + publishEntryDefinition.AppId.NotNullOrEmpty().ToLowerInvariant() + ArtifactNameSeparator + publishEntryDefinition.Id.ToUpperInvariant();
                    var publishArtifactTempPath = TemporaryDirectory / publishArtifactName;
                    var publishArtifactFilePath = CommonArtifactsUploadDirectory / $"{publishArtifactName}.zip";
                    publishArtifactTempPath.CreateOrCleanDirectory();
                    publishArtifactFilePath.DeleteFile();
                    await releaseAssetsDir.MoveTo(publishArtifactTempPath);
                    publishArtifactTempPath.ZipTo(publishArtifactFilePath);
                    break;
            }
        });
    }

    private Task TestAppEntries(AllEntry allEntry, PipelineRun pipeline, IEnumerable<string> idsToRun)
    {
        var pipelinePreSetup = pipeline.Pipeline.GetPipelinePreSetup();

        IEnumerable<IRunEntryDefinition> entriesToRun;

        if (!idsToRun.Any())
        {
            entriesToRun = allEntry.TestEntryDefinitionMap.Values.Cast<IRunEntryDefinition>();
        }
        else
        {
            entriesToRun = allEntry.TestEntryDefinitionMap.Values.Where(i => idsToRun.Any(j => j.Equals(i.Id)));
        }

        return RunEntry(allEntry, pipeline, entriesToRun, pipelinePreSetup);
    }

    private Task BuildAppEntries(AllEntry allEntry, PipelineRun pipeline, IEnumerable<string> idsToRun)
    {
        var pipelinePreSetup = pipeline.Pipeline.GetPipelinePreSetup();

        IEnumerable<IRunEntryDefinition> entriesToRun;

        if (!idsToRun.Any())
        {
            entriesToRun = allEntry.BuildEntryDefinitionMap.Values.Cast<IRunEntryDefinition>();
        }
        else
        {
            entriesToRun = allEntry.BuildEntryDefinitionMap.Values.Where(i => idsToRun.Any(j => j.Equals(i.Id)));
        }

        return RunEntry(allEntry, pipeline, entriesToRun, pipelinePreSetup);
    }

    private Task PublishAppEntries(AllEntry allEntry, PipelineRun pipeline, IEnumerable<string> idsToRun)
    {
        var pipelinePreSetup = pipeline.Pipeline.GetPipelinePreSetup();

        IEnumerable<IRunEntryDefinition> entriesToRun;

        if (!idsToRun.Any())
        {
            entriesToRun = allEntry.PublishEntryDefinitionMap.Values.Cast<IRunEntryDefinition>();
        }
        else
        {
            entriesToRun = allEntry.PublishEntryDefinitionMap.Values.Where(i => idsToRun.Any(j => j.Equals(i.Id)));
        }

        return RunEntry(allEntry, pipeline, entriesToRun, pipelinePreSetup);
    }

    private void ValidateBumpVersion(AllVersions allVersions, string? bumpVersion)
    {
        if (!SemVersion.TryParse(bumpVersion, SemVersionStyles.Strict, out var inputVersion))
        {
            throw new Exception("Invalid semver version");
        }

        // Fail if current branch is not on the proper bump branch
        string env;
        if (inputVersion.IsPrerelease)
        {
            if (!Repository.Branch.Equals(inputVersion.PrereleaseIdentifiers[0], StringComparison.InvariantCultureIgnoreCase))
            {
                throw new Exception($"{inputVersion} should bump on {inputVersion.PrereleaseIdentifiers[0]} branch");
            }
            env = inputVersion.PrereleaseIdentifiers[0];
        }
        else
        {
            if (!Repository.Branch.Equals(MainEnvironmentBranch, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new Exception($"{inputVersion} should bump on {MainEnvironmentBranch.ToLowerInvariant()} branch");
            }
            env = MainEnvironmentBranch.ToLowerInvariant();
        }

        if (allVersions.EnvVersionGrouped.TryGetValue(env, out List<SemVersion>? value))
        {
            var lastVersion = value.Last();
            // Fail if the version is already released
            if (SemVersion.ComparePrecedence(lastVersion, inputVersion) == 0)
            {
                throw new Exception($"The latest version in the {env} releases is already {inputVersion}");
            }
            // Fail if the version is behind the latest release
            if (SemVersion.ComparePrecedence(lastVersion, inputVersion) > 0)
            {
                throw new Exception($"{inputVersion} is behind the latest version {lastVersion} in the {env} releases");
            }
        }
    }

    private async Task<List<(AppEntry AppEntry, SemVersion BumpVersion)>> InteractiveRelease()
    {
        Prompt.ColorSchema.Answer = ConsoleColor.Green;
        Prompt.ColorSchema.Select = ConsoleColor.DarkMagenta;
        Prompt.Symbols.Prompt = new Symbol("?", "?");
        Prompt.Symbols.Done = new Symbol("✓", "✓");
        Prompt.Symbols.Error = new Symbol("x", "x");

        if (!EnvironmentBranches.Any(i => i.Equals(Repository.Branch, StringComparison.InvariantCultureIgnoreCase)))
        {
            Assert.Fail($"{Repository.Branch} is not on environment branches");
        }

        var allEntry = await ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this));

        string currentEnvIdentifier = Repository.Branch.ToLowerInvariant();

        ObjectHolder<IReadOnlyCollection<Output>> lsRemote = new();

        List<(AppEntry? AppEntry, AllVersions? AllVersions)> appEntryVersions = [];

        foreach (var pair in allEntry.AppEntryMap)
        {
            string appId = pair.Key;

            var allVersions = await ValueHelpers.GetOrFail(() => EntryHelpers.GetAllVersions(this, allEntry, appId, lsRemote));

            appEntryVersions.Add((pair.Value, allVersions));
        }

        List<string> appEntryIdHasBump = [];
        foreach (var appEntryVersion in appEntryVersions)
        {
            if (appEntryVersion.AppEntry != null &&
                appEntryVersion.AllVersions != null &&
                appEntryVersion.AllVersions.EnvVersionGrouped.TryGetValue(currentEnvIdentifier, out var currentEnvVersions) &&
                currentEnvVersions.LastOrDefault() is SemVersion currentEnvLatestVersion &&
                appEntryVersion.AllVersions.VersionCommitPaired.TryGetValue(currentEnvLatestVersion, out var currentEnvLatestVersionCommitId) &&
                currentEnvLatestVersionCommitId == Repository.Commit)
            {
                appEntryIdHasBump.Add(appEntryVersion.AppEntry.AppId);
                Console.Write("  Commit has already bumped ");
                ConsoleHelpers.WriteWithColor(appEntryVersion.AppEntry.AppId, ConsoleColor.DarkMagenta);
                Console.WriteLine();
            }
        }

        List<(AppEntry AppEntry, SemVersion BumpVersion)> appEntryVersionsToBump = [];

        appEntryVersions.Add((null, null));

        while (true)
        {
            var availableBump = appEntryVersions
                .Where(i =>
                {
                    if (appEntryVersionsToBump.Any(j => j.AppEntry.AppId.Equals(i.AppEntry?.AppId, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        return false;
                    }
                    if (appEntryIdHasBump.Any(j => j.Equals(i.AppEntry?.AppId, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        return false;
                    }

                    return true;
                });

            (AppEntry? AppEntry, AllVersions? AllVersions) appEntryVersion = (null, null);

            if (appEntryVersionsToBump.Count == 0 && availableBump.Count() == 2)
            {
                appEntryVersion = availableBump.FirstOrDefault();
                Console.Write("  App id to bump: ");
                ConsoleHelpers.WriteWithColor(appEntryVersion.AppEntry?.AppId!, ConsoleColor.Green);
                Console.WriteLine("");
            }
            else if (availableBump.Count() > 1)
            {
                appEntryVersion = Prompt.Select("App id to bump", availableBump, textSelector: (appEntry) => appEntry.AppEntry == null ? "->done" : appEntry.AppEntry.AppId);
            }

            if (appEntryVersion.AppEntry == null || appEntryVersion.AllVersions == null)
            {
                if (appEntryVersionsToBump.Count != 0)
                {
                    var answer = Prompt.Confirm("Are you sure to bump selected version(s)?", defaultValue: false);
                    if (answer)
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
                continue;
            }

            var currentEnvLatestVersion = appEntryVersion.AllVersions.EnvVersionGrouped[currentEnvIdentifier].Last();
            Console.Write("  Current latest version: ");
            ConsoleHelpers.WriteWithColor(currentEnvLatestVersion.ToString() ?? "null", ConsoleColor.Green);
            Console.WriteLine("");

            string[] versionParts;
            if (currentEnvIdentifier.Equals(MainEnvironmentBranch, StringComparison.InvariantCultureIgnoreCase))
            {
                versionParts = ["Major", "Minor", "Patch", "Manual input"];
            }
            else
            {
                versionParts = ["Major", "Minor", "Patch", "Prerelease", "Manual input"];
            }

            var versionPartToBump = Prompt.Select("Version part to bump", versionParts);

            if (versionPartToBump.Equals("Manual input", StringComparison.InvariantCultureIgnoreCase))
            {
                List<Func<object?, ValidationResult?>> validators =
                [
                    Validators.Required(),
                    (input => {
                        try
                        {
                            if (input == null)
                            {
                                throw new Exception("Manual input is null");
                            }
                            ValidateBumpVersion(appEntryVersion.AllVersions, input.ToString());
                        }
                        catch (Exception ex)
                        {
                            return new ValidationResult(ex.Message);
                        }

                        return ValidationResult.Success;
                    })
                ];

                var bumpVersionStr = await Task.Run(() => Prompt.Input<string>("Input new version", validators: validators));
                var bumpVersion = SemVersion.Parse(bumpVersionStr, SemVersionStyles.Strict);
                appEntryVersionsToBump.Add((appEntryVersion.AppEntry, bumpVersion));
            }
            else
            {
                var part = versionPartToBump.ToLowerInvariant() switch
                {
                    "major" => VersionPart.Major,
                    "minor" => VersionPart.Minor,
                    "patch" => VersionPart.Patch,
                    "prerelease" => VersionPart.Prerelease,
                    _ => throw new NotImplementedException($"Version part {versionPartToBump} is not implemented"),
                };
                VersionBump versionBump = new()
                {
                    Part = part,
                    IsIncrement = true,
                    BumpAssign = 1,
                    Rank = 0
                };
                var bumpVersion = currentEnvLatestVersion.ApplyBumps([versionBump]);
                appEntryVersionsToBump.Add((appEntryVersion.AppEntry, bumpVersion));

                Console.Write("  New version: ");
                ConsoleHelpers.WriteWithColor(bumpVersion.ToString() ?? "null", ConsoleColor.Green);
                Console.WriteLine("");
            }
        }

        return appEntryVersionsToBump;
    }

    private async Task RunBump(AllEntry allEntry, Dictionary<string, SemVersion> bumpMap)
    {
        if (bumpMap.Count == 0)
        {
            Log.Information("No version selected to bump.");
        }

        List<string> tagsToPush = [];

        foreach (var bumpPair in bumpMap)
        {
            if (!allEntry.AppEntryMap.ContainsKey(bumpPair.Key))
            {
                throw new Exception("No app entry for " + bumpPair.Key);
            }

            tagsToPush.Add(bumpPair.Key.ToLowerInvariant() + "/" + bumpPair.Value.ToString() + "-bump");
        }

        foreach (var tag in tagsToPush)
        {
            Git.Invoke($"tag {tag}", logInvocation: false, logOutput: false);
        }

        // ---------- Apply bump ----------

        await Task.Run(() =>
        {
            Git.Invoke("push origin HEAD", logInvocation: false, logOutput: false);
            Git.Invoke("push origin " + tagsToPush.Select(t => "refs/tags/" + t).Join(" "), logInvocation: false, logOutput: false);

            var bumpTag = "bump-" + Repository.Branch.ToLowerInvariant();
            try
            {
                Git.Invoke("push origin :refs/tags/" + bumpTag, logInvocation: false, logOutput: false);
            }
            catch { }
            Git.Invoke("tag --force " + bumpTag, logInvocation: false, logOutput: false);
            Git.Invoke("push origin --force " + bumpTag, logInvocation: false, logOutput: false);
        });
    }

    private async Task<Dictionary<string, SemVersion>> RunBumpArgsOrInteractive()
    {
        CheckEnvironementBranches();

        ValueHelpers.GetOrFail(() => SplitArgs, out var splitArgs);

        var allEntry = await ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this));

        CheckAppEntry(allEntry);
        
        if (await allEntry.WorkflowConfigEntryDefinition.GetUseJsonFileVersioning())
        {
            throw new Exception("Bump is disabled if UseJsonFileVersioning is enabled");
        }

        string currentEnvIdentifier = Repository.Branch.ToLowerInvariant();

        ObjectHolder<IReadOnlyCollection<Output>> lsRemote = new();

        Dictionary<string, string?> argsBumps = [];

        if (splitArgs.Count == 1 && !allEntry.AppEntryMap.ContainsKey(splitArgs.First().Key) && splitArgs.First().Value.IsNullOrEmpty())
        {
            if (allEntry.AppEntryMap.Count != 1)
            {
                throw new Exception($"Redacted appId args is not valid for multiple app entries.");
            }
            argsBumps[allEntry.AppEntryMap.First().Value.AppId] = splitArgs.First().Key;
        }
        else
        {
            argsBumps = splitArgs.ToDictionary();
        }

        Dictionary<string, SemVersion> bumpMap = [];
        foreach (var argsBump in argsBumps)
        {
            string appId = argsBump.Key.ToLower();

            ValueHelpers.GetOrFail(appId, allEntry, out var appEntry);
            var allVersions = await ValueHelpers.GetOrFail(() => EntryHelpers.GetAllVersions(this, allEntry, appId, lsRemote));

            if (allVersions.EnvVersionGrouped.TryGetValue(currentEnvIdentifier, out var currentEnvVersions) &&
                currentEnvVersions.LastOrDefault() is SemVersion currentEnvLatestVersion &&
                allVersions.VersionCommitPaired.TryGetValue(currentEnvLatestVersion, out var currentEnvLatestVersionCommitId) &&
                currentEnvLatestVersionCommitId == Repository.Commit)
            {
                throw new Exception($"Commit has already bumped {appId}");
            }

            SemVersion latestVersion = allVersions.EnvVersionGrouped[currentEnvIdentifier]?.LastOrDefault()!;
            SemVersion bumpVersion = latestVersion.Clone();

            var bumps = argsBump.Value.NotNullOrEmpty().Trim().ToLowerInvariant().Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(bumpPart =>
                {
                    try
                    {
                        bool isIncrement = true;
                        string[] bumpValue = [];
                        if (bumpPart.Contains('+'))
                        {
                            isIncrement = true;
                            bumpValue = bumpPart.Split("+");
                        }
                        else if (bumpPart.Contains('>'))
                        {
                            isIncrement = false;
                            bumpValue = bumpPart.Split(">");
                        }
                        else
                        {
                            bumpValue = [bumpPart];
                        }
                        int bumpAssign = bumpValue.Length > 1 ? int.Parse(bumpValue[1]) : 1;
                        VersionPart bumpVersionPart;
                        int rank;
                        switch (bumpValue[0])
                        {
                            case "major":
                                bumpVersionPart = VersionPart.Major;
                                rank = isIncrement ? 6 : 7;
                                break;
                            case "minor":
                                bumpVersionPart = VersionPart.Minor;
                                rank = isIncrement ? 4 : 5;
                                break;
                            case "patch":
                                bumpVersionPart = VersionPart.Patch;
                                rank = isIncrement ? 2 : 3;
                                break;
                            case "prerelease":
                            case "pre":
                                bumpVersionPart = VersionPart.Prerelease;
                                rank = isIncrement ? 0 : 1;
                                break;
                            default:
                                throw new ArgumentException("Invalid bump value " + argsBump.Value);
                        }

                        return new VersionBump()
                        {
                            Part = bumpVersionPart,
                            IsIncrement = isIncrement,
                            BumpAssign = bumpAssign,
                            Rank = rank
                        };
                    }
                    catch
                    {
                        throw new ArgumentException("Invalid bump value " + argsBump.Value);
                    }
                })
                .OrderByDescending(i => i.Rank);

            bumpVersion = bumpVersion.ApplyBumps(bumps);

            ValidateBumpVersion(allVersions, bumpVersion.ToString());

            bumpMap[appId] = bumpVersion;

            Log.Information("Bump {appId} from {latestVersion} to {bumpVersion}", appId, latestVersion, bumpVersion);
        }

        if (bumpMap.Count == 0)
        {
            bumpMap = (await InteractiveRelease()).ToDictionary(i => i.AppEntry.AppId, i => i.BumpVersion);
        }

        await RunBump(allEntry, bumpMap);

        return bumpMap;
    }

    private async Task StartStatusWatch(bool cancelOnDone = false, params (string AppId, string Environment)[] appIds)
    {
        var allEntry = await ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this));

        ConsoleTableHeader[] headers =
            [
                ("App EntryId", HorizontalAlignment.Right),
                ("Environment", HorizontalAlignment.Center),
                ("Version", HorizontalAlignment.Right),
                ("Status", HorizontalAlignment.Center)
            ];

        CancellationTokenSource cts = new();
        Console.CancelKeyPress += delegate {
            cts.Cancel();
        };

        int lines = 0;

        while (!cts.IsCancellationRequested)
        {
            List<ConsoleTableRow> rows = [];

            ObjectHolder<IReadOnlyCollection<Output>> lsRemote = new();

            bool allDone = true;
            bool pullFailed = false;

            List<(string AppId, string Environment)> appIdsPassed = [];
            List<(string AppId, string Environment)> appIdsFailed = [];

            foreach (var appEntry in allEntry.AppEntryMap.Values)
            {
                AllVersions allVersions;
                try
                {
                    allVersions = await ValueHelpers.GetOrFail(() => EntryHelpers.GetAllVersions(this, allEntry, appEntry.AppId, lsRemote));
                }
                catch
                {
                    pullFailed = true;
                    allDone = false;
                    break;
                }

                bool firstEntryRow = true;

                ConsoleColor statusColor = ConsoleColor.DarkGray;

                if (allVersions.EnvSorted.Count != 0)
                {
                    foreach (var env in allVersions.EnvSorted)
                    {
                        var bumpedVersion = allVersions.EnvVersionGrouped[env].Last();
                        string published;
                        if (allVersions.VersionFailed.Contains(bumpedVersion))
                        {
                            published = "Run Failed";
                            statusColor = ConsoleColor.Red;
                            appIdsFailed.Add((appEntry.AppId.ToLowerInvariant(), env.ToLowerInvariant()));
                        }
                        else if (allVersions.VersionPassed.Contains(bumpedVersion))
                        {
                            published = "Published";
                            statusColor = ConsoleColor.Green;
                            appIdsPassed.Add((appEntry.AppId.ToLowerInvariant(), env.ToLowerInvariant()));
                        }
                        else if (allVersions.VersionQueue.Contains(bumpedVersion))
                        {
                            published = "Publishing";
                            statusColor = ConsoleColor.DarkYellow;
                            allDone = false;
                        }
                        else if (allVersions.VersionBump.Contains(bumpedVersion))
                        {
                            published = "Waiting for queue";
                            statusColor = ConsoleColor.DarkYellow;
                            allDone = false;
                        }
                        else
                        {
                            published = "Not published";
                            statusColor = ConsoleColor.DarkGray;
                            allDone = false;
                        }
                        var bumpedVersionStr = SemverHelpers.IsVersionEmpty(bumpedVersion) ? "-" : bumpedVersion.ToString();
                        rows.Add(ConsoleTableRow.FromValue(
                            [
                                (firstEntryRow ? appEntry.AppId : "", ConsoleColor.Magenta),
                                (env, ConsoleColor.Magenta),
                                (bumpedVersionStr, ConsoleColor.Magenta),
                                (published, statusColor)
                            ]));
                        firstEntryRow = false;
                    }
                }
                else
                {
                    rows.Add(ConsoleTableRow.FromValue(
                        [
                            (appEntry.AppId, ConsoleColor.Magenta),
                            (null, ConsoleColor.Magenta),
                            (null, ConsoleColor.Magenta),
                            ("Not published", statusColor)
                        ]));
                }
                rows.Add(ConsoleTableRow.Separator);
            }
            if (rows.Count != 0)
            {
                rows.RemoveAt(rows.Count - 1);
            }

            Console.SetCursorPosition(0, int.Max(Console.CursorTop - lines, 0));

            if (pullFailed)
            {
                ConsoleHelpers.ClearCurrentConsoleLine();
                Console.Write("Time: " + DateTime.Now);
                Console.Write(", ");
                ConsoleHelpers.WriteWithColor("Error: Connection problems", ConsoleColor.Red);
                Console.WriteLine();
                lines = 0;
            }
            else
            {
                ConsoleHelpers.WriteLineClean("Time: " + DateTime.Now);
                lines = ConsoleTableHelpers.LogInfoTableWatch(headers, [.. rows]);
            }
            lines += 1;

            if (cancelOnDone)
            {
                if (allDone && appIds.Length == 0)
                {
                    break;
                }
                if (appIds.Any(appIdsFailed.Contains))
                {
                    Assert.Fail("Pipeline run has failed.");
                    break;
                }
                if (appIds.All(appIdsPassed.Contains))
                {
                    break;
                }
            }

            await Task.Delay(1000, cts.Token);
        }
    }

    private async Task RunInteractive()
    {
        Prompt.ColorSchema.Answer = ConsoleColor.Green;
        Prompt.ColorSchema.Select = ConsoleColor.DarkMagenta;
        Prompt.Symbols.Prompt = new Symbol("?", "?");
        Prompt.Symbols.Done = new Symbol("✓", "✓");
        Prompt.Symbols.Error = new Symbol("x", "x");

        CheckEnvironementBranches();

        ValueHelpers.GetOrFail(() => SplitArgs, out var splitArgs);

        var allEntry = await ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this));

        CheckAppEntry(allEntry);

        static void printHead(string name)
        {
            Console.WriteLine();
            Console.WriteLine( "╬════════════════");
            Console.WriteLine($"║ {name}");
            Console.WriteLine( "╬═══════");
            Console.WriteLine();
        }

        Dictionary<string, BuildOption> taskOptions = [];

        taskOptions[nameof(Version)] = new()
        {
            Name = nameof(Version),
            DisplayText = nameof(Version),
            Execute = () => Task.Run(async () =>
            {
                printHead("Fetch");
                await RunFetch();

                printHead("Version");
                await RunVersion();
            })
        };

        taskOptions[nameof(Run)] = new()
        {
            Name = nameof(Run),
            DisplayText = nameof(Run),
            Execute = () => Task.Run(async () =>
            {
                printHead("Run");

                AppEntry appEntry;
                if (allEntry.AppEntryMap.Count > 1)
                {
                    appEntry = Prompt.Select("App ID to run", allEntry.AppEntryMap.Values, textSelector: (appEntry) => appEntry.AppId);
                }
                else if (allEntry.AppEntryMap.Count == 1)
                {
                    appEntry = allEntry.AppEntryMap.First().Value;
                    Console.Write("  App ID to run ");
                    ConsoleHelpers.WriteWithColor(appEntry.AppId, ConsoleColor.DarkMagenta);
                    Console.WriteLine();
                }
                else
                {
                    throw new Exception("No app entry configured");
                }

                static string runEntryDefinitionTextSelector(IRunEntryDefinition definition)
                {
                    return definition switch
                    {
                        IBuildEntryDefinition buildEntryDefinition => $"{buildEntryDefinition.Id} (BuildEntry)",
                        ITestEntryDefinition testEntryDefinition => $"{testEntryDefinition.Id} (TestEntry)",
                        IPublishEntryDefinition publishEntryDefinition => $"{publishEntryDefinition.Id} (PublishEntry)",
                        _ => throw new NotImplementedException(definition.GetType().Name),
                    };
                }

                IRunEntryDefinition runEntryDefinition;
                if (appEntry.RunEntryDefinitions.Count > 1)
                {
                    runEntryDefinition = Prompt.Select("App entry to run", appEntry.RunEntryDefinitions, textSelector: runEntryDefinitionTextSelector);
                }
                else if (appEntry.RunEntryDefinitions.Count == 1)
                {
                    runEntryDefinition = appEntry.RunEntryDefinitions.First();
                    Console.Write("  App entry to run ");
                    ConsoleHelpers.WriteWithColor(runEntryDefinitionTextSelector(runEntryDefinition), ConsoleColor.DarkMagenta);
                    Console.WriteLine();
                }
                else
                {
                    throw new Exception("No app entry configured");
                }

                var pipeline = PipelineHelpers.SetupPipeline(this);

                Console.WriteLine();

                await RunEntry(allEntry, pipeline, [runEntryDefinition], null);
            })
        };

        if (!await allEntry.WorkflowConfigEntryDefinition.GetUseJsonFileVersioning())
        {
            taskOptions[nameof(Bump)] = new()
            {
                Name = nameof(Bump),
                DisplayText = nameof(Bump),
                Execute = () => Task.Run(async () =>
                {
                    printHead("Fetch");
                    await RunFetch();

                    printHead("Version");
                    await RunVersion();

                    printHead("BumpAndForget");
                    var bumpMap = await RunBumpArgsOrInteractive();
                    Console.WriteLine();
                    await StartStatusWatch(true, [.. bumpMap.Select(i => (i.Key, Repository.Branch))]);
                })
            };

            taskOptions[nameof(BumpAndForget)] = new()
            {
                Name = nameof(BumpAndForget),
                DisplayText = nameof(BumpAndForget),
                Execute = () => Task.Run(async () =>
                {
                    printHead("Fetch");
                    await RunFetch();

                    printHead("Version");
                    await RunVersion();

                    printHead("BumpAndForget");
                    await RunBumpArgsOrInteractive();
                })
            };
        }

        taskOptions[nameof(StatusWatch)] = new()
        {
            Name = nameof(StatusWatch),
            DisplayText = nameof(StatusWatch),
            Execute = () => Task.Run(async () =>
            {
                printHead("StatusWatch");

                Log.Information("Commit: {Value}", Repository.Commit);
                Log.Information("Branch: {Value}", Repository.Branch);

                Console.WriteLine();

                await StartStatusWatch(false);
            })
        };

        taskOptions[nameof(GithubWorkflow)] = new()
        {
            Name = nameof(GithubWorkflow),
            DisplayText = nameof(GithubWorkflow),
            Execute = () => Task.Run(async () =>
            {
                printHead("GithubWorkflow");

                await PipelineHelpers.BuildWorkflow<GithubPipeline>(this, allEntry);
            })
        };

        taskOptions[nameof(AzureWorkflow)] = new()
        {
            Name = nameof(AzureWorkflow),
            DisplayText = nameof(AzureWorkflow),
            Execute = () => Task.Run(async () =>
            {
                printHead("AzureWorkflow");

                await PipelineHelpers.BuildWorkflow<AzurePipeline>(this, allEntry);
            })
        };

        var taskToRun = Prompt.Select("Task to run", taskOptions, textSelector: taskOption => taskOption.Value.DisplayText);

        await taskToRun.Value.Execute.Invoke();
    }

    private Task RunFetch()
    {
        return Task.Run(() =>
        {
            Log.Information("Fetching...");
            Git.Invoke("fetch --prune --prune-tags --force", logInvocation: false, logOutput: false);
        });
    }

    private async Task RunVersion()
    {
        var allEntry = await ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this));

        CheckAppEntry(allEntry);

        Log.Information("Commit: {Value}", Repository.Commit);
        Log.Information("Branch: {Value}", Repository.Branch);

        ConsoleTableHeader[] headers =
        [
            ("App EntryId", HorizontalAlignment.Right),
                ("Environment", HorizontalAlignment.Center),
                ("Bumped Version", HorizontalAlignment.Right),
                ("Published", HorizontalAlignment.Center)
        ];
        List<ConsoleTableRow> rows = [];

        ObjectHolder<IReadOnlyCollection<Output>> lsRemote = new();

        foreach (var key in allEntry.AppEntryMap.Select(i => i.Key))
        {
            string appId = key;

            ValueHelpers.GetOrFail(appId, allEntry, out var appEntry);

            var allVersions = await ValueHelpers.GetOrFail(() => EntryHelpers.GetAllVersions(this, allEntry, appId, lsRemote));

            if (await allEntry.WorkflowConfigEntryDefinition.GetUseJsonFileVersioning())
            {
                EntryHelpers.VerifyVersionsFile(allVersions, appId, EnvironmentBranches);
            }

            bool firstEntryRow = true;

            if (allVersions.EnvSorted.Count != 0)
            {
                foreach (var env in allVersions.EnvSorted)
                {
                    var bumpedVersion = allVersions.EnvVersionGrouped[env].Last();
                    allVersions.EnvLatestVersionPaired.TryGetValue(env, out var releasedVersion);
                    var published = "yes";
                    if (releasedVersion == null)
                    {
                        published = "no";
                    }
                    else if (bumpedVersion != releasedVersion)
                    {
                        published = releasedVersion + "*";
                    }
                    var bumpedVersionStr = SemverHelpers.IsVersionEmpty(bumpedVersion) ? "-" : bumpedVersion.ToString();
                    rows.Add(ConsoleTableRow.FromValue([firstEntryRow ? appId : "", env, bumpedVersionStr, published]));
                    firstEntryRow = false;
                }
            }
            else
            {
                rows.Add(ConsoleTableRow.FromValue([appId, default(string), default(string), "no"]));
            }
            rows.Add(ConsoleTableRow.Separator);
        }
        rows.RemoveAt(rows.Count - 1);

        Console.WriteLine();

        ConsoleTableHelpers.LogInfoTable(headers, [.. rows]);
    }
}
