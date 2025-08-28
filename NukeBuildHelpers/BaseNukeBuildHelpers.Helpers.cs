using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Git;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Common.Models;
using NukeBuildHelpers.ConsoleInterface;
using NukeBuildHelpers.ConsoleInterface.Enums;
using NukeBuildHelpers.ConsoleInterface.Models;
using NukeBuildHelpers.Entry.Definitions;
using NukeBuildHelpers.Entry.Enums;
using NukeBuildHelpers.Entry.Extensions;
using NukeBuildHelpers.Entry.Helpers;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.Pipelines.Azure;
using NukeBuildHelpers.Pipelines.Common;
using NukeBuildHelpers.Pipelines.Common.Models;
using NukeBuildHelpers.Pipelines.Github;
using NukeBuildHelpers.RunContext.Models;
using Octokit;
using Semver;
using Serilog;
using Sharprompt;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;

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

    private static void OutputBump(string appId)
    {
        var appOut = CommonOutputDirectory / appId.ToLowerInvariant();
        var runtimeOut = appOut / "runtime";

        if (!runtimeOut.DirectoryExists())
        {
            runtimeOut.CreateDirectory();
        }

        (appOut / "stamp").WriteAllText(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
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

    private void SetupRunContext(IRunEntryDefinition targetEntry, List<(AppVersion AppVersion, RunType RunType)> appSetup, string? releaseNotes, PipelineRun pipeline)
    {
        Dictionary<string, AppRunContext> versions = [];

        foreach (var (appVersion, runType) in appSetup)
        {
            if (runType == RunType.Local)
            {
                versions[appVersion.AppId] = new AppRunContext()
                {
                    AppId = appVersion.AppId,
                    RunType = RunType.Local,
                    AppVersion = appVersion,
                    BumpVersion = null,
                    PullRequestVersion = null
                };
            }
            else if (runType == RunType.Bump)
            {
                if (releaseNotes == null)
                {
                    throw new Exception("Release notes is required for bump run");
                }
                versions[appVersion.AppId] = new AppRunContext()
                {
                    AppId = appVersion.AppId,
                    RunType = RunType.Bump,
                    AppVersion = appVersion,
                    BumpVersion = new BumpReleaseVersion()
                    {
                        AppId = appVersion.AppId,
                        Environment = appVersion.Environment,
                        Version = appVersion.Version,
                        BuildId = appVersion.BuildId,
                        ReleaseNotes = releaseNotes
                    },
                    PullRequestVersion = null
                };
            }
            else if (runType == RunType.PullRequest)
            {
                if (pipeline.PipelineInfo.PullRequestNumber == null)
                {
                    throw new Exception("Pull request number is required for pull request run");
                }
                versions[appVersion.AppId] = new AppRunContext()
                {
                    AppId = appVersion.AppId,
                    RunType = RunType.PullRequest,
                    AppVersion = appVersion,
                    BumpVersion = null,
                    PullRequestVersion = new PullRequestReleaseVersion()
                    {
                        AppId = appVersion.AppId,
                        Environment = appVersion.Environment,
                        Version = appVersion.Version,
                        BuildId = appVersion.BuildId,
                        PullRequestNumber = pipeline.PipelineInfo.PullRequestNumber.Value
                    }
                };
            }
            else
            {
                versions[appVersion.AppId] = new AppRunContext()
                {
                    AppId = appVersion.AppId,
                    RunType = runType,
                    AppVersion = appVersion,
                    BumpVersion = null,
                    PullRequestVersion = null
                };
            }
        }

        targetEntry.RunContext = new RunContext.Models.RunContext()
        {
            PipelineType = PipelineType,
            Apps = versions.AsReadOnly()
        };
    }

    private async Task EntryPreSetup(AllEntry allEntry, PipelineRun pipeline, PipelinePreSetup pipelinePreSetup)
    {
        EntryHelpers.SetupSecretVariables(this);

        PipelineType = pipeline.PipelineType;

        string? releaseNotes = null;

        if (pipelinePreSetup.HasRelease)
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
            List<(AppVersion AppVersion, RunType RunType)> appSetup = [];

            if (!pipelinePreSetup.EntrySetupMap.TryGetValue(ValueHelpers.GetOrNullFail(targetEntry.Id), out var entrySetup))
            {
                throw new Exception("Entry id not found");
            }

            foreach (var appId in targetEntry.AppIds)
            {
                if (!entrySetup.RunTypes.TryGetValue(ValueHelpers.GetOrNullFail(appId), out var runType))
                {
                    throw new Exception("App id not found");
                }

                if (!pipelinePreSetup.AppRunEntryMap.TryGetValue(ValueHelpers.GetOrNullFail(appId), out var entry))
                {
                    throw new Exception("App id not found");
                }

                appSetup.Add((new AppVersion()
                {
                    AppId = entry.AppId.NotNullOrEmpty(),
                    Environment = entry.Environment,
                    Version = ParseSemVersion(entry),
                    BuildId = pipelinePreSetup.BuildId
                }, runType));
            }

            SetupRunContext(targetEntry, appSetup, releaseNotes, pipeline);
        }

        foreach (var dependentEntry in allEntry.DependentEntryDefinitionMap.Values)
        {
            List<(AppVersion AppVersion, RunType RunType)> appSetup = [];

            if (!pipelinePreSetup.EntrySetupMap.TryGetValue(ValueHelpers.GetOrNullFail(dependentEntry.Id), out var entrySetup))
            {
                throw new Exception("Entry id not found");
            }

            foreach (var appId in dependentEntry.AppIds)
            {
                if (!entrySetup.RunTypes.TryGetValue(ValueHelpers.GetOrNullFail(appId), out var runType))
                {
                    throw new Exception("App id not found");
                }

                if (!pipelinePreSetup.AppRunEntryMap.TryGetValue(ValueHelpers.GetOrNullFail(appId), out var entry))
                {
                    throw new Exception("App id not found");
                }

                appSetup.Add((new AppVersion()
                {
                    AppId = entry.AppId.NotNullOrEmpty(),
                    Environment = entry.Environment,
                    Version = ParseSemVersion(entry),
                    BuildId = pipelinePreSetup.BuildId
                }, runType));

            }

            SetupRunContext(dependentEntry, appSetup, releaseNotes, pipeline);
        }
    }

    private async Task RunEntry(AllEntry allEntry, PipelineRun pipeline, IEnumerable<IRunEntryDefinition> entriesToRun, PipelinePreSetup pipelinePreSetup, Func<IRunEntryDefinition, Task>? preExecute, Func<IRunEntryDefinition, Task>? postExecute)
    {
        await pipeline.Pipeline.PrepareEntryRun(allEntry, pipelinePreSetup, entriesToRun.ToDictionary(i => i.Id));

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

            CommonOutputDirectory.CreateOrCleanDirectory();

            CacheBump();

            await CachePreload(entry);

            foreach (var appId in entry.AppIds)
                OutputBump(appId);
            OutputBump("$common");

            if (preExecute != null)
            {
                await preExecute.Invoke(entry);
            }

            await entry.GetExecute();

            if (postExecute != null)
            {
                await postExecute.Invoke(entry);
            }

            await CachePostload(entry);
        }

        await pipeline.Pipeline.FinalizeEntryRun(allEntry, pipelinePreSetup, entriesToRun.ToDictionary(i => i.Id));
    }

    private Task RunEntry(AllEntry allEntry, PipelineRun pipeline, IEnumerable<IRunEntryDefinition> entriesToRun, PipelinePreSetup pipelinePreSetup)
    {
        return RunEntry(allEntry, pipeline, entriesToRun, pipelinePreSetup, static entry =>
        {
            UnpackArtifacts([.. entry.AppIds]);
            UnpackArtifacts(["$common"]);
            return Task.CompletedTask;

        }, PackArtifacts);
    }

    private static async Task PackArtifacts(IRunEntryDefinition entry)
    {
        async Task Pack(string appId)
        {
            string entryType;
            switch (entry)
            {
                case ITestEntryDefinition testEntryDefinition:
                    if (await testEntryDefinition.ExecuteBeforeBuild())
                        entryType = "01_pre_test";
                    else
                        entryType = "03_post_test";
                    break;
                case IBuildEntryDefinition:
                    entryType = "02_build";
                    break;
                case IPublishEntryDefinition:
                    entryType = "04_publish";
                    break;
                default:
                    throw new NotSupportedException($"Entry not supported '{entry.GetType().Name}'");
            }
            var appIdLower = appId.NotNullOrEmpty().ToLowerInvariant();
            var artifactName = entryType + ArtifactNameSeparator + appIdLower + ArtifactNameSeparator + entry.Id.ToUpperInvariant();
            var artifactTempPath = TemporaryDirectory / artifactName;
            var artifactFilePath = CommonArtifactsUploadDirectory / appIdLower / $"{artifactName}.zip";
            artifactTempPath.CreateOrCleanDirectory();
            artifactFilePath.DeleteFile();
            await (CommonOutputDirectory / appIdLower / "runtime").MoveTo(artifactTempPath);
            artifactTempPath.ZipTo(artifactFilePath);
            Log.Information("Created artifact {artifactFilePath}", artifactFilePath);
        }
        foreach (var appId in entry.AppIds)
        {
            await Pack(appId);
        }
        await Pack("$common");
    }

    private static void UnpackArtifacts(params string[]? appIds)
    {
        if (CommonArtifactsDirectory.DirectoryExists())
        {
            foreach (var artifact in CommonArtifactsDirectory.GetFiles())
            {
                if (!artifact.HasExtension(".zip"))
                {
                    continue;
                }
                var appId = artifact.Name.Split(ArtifactNameSeparator).Skip(1).FirstOrDefault().NotNullOrEmpty().ToLowerInvariant();
                if (appIds == null || 
                    appIds.Length == 0 ||
                    appIds.Any(i => i.Equals(appId, StringComparison.InvariantCultureIgnoreCase)))
                {
                    artifact.UnZipTo(CommonOutputDirectory / appId / "runtime");
                }
            }
        }
    }

    private async Task TestAppEntries(AllEntry allEntry, PipelineRun pipeline, IEnumerable<string> idsToRun)
    {
        var pipelinePreSetup = await pipeline.Pipeline.GetPipelinePreSetup();

        IEnumerable<IRunEntryDefinition> entriesToRun;

        if (!idsToRun.Any())
        {
            entriesToRun = allEntry.TestEntryDefinitionMap.Values.Cast<IRunEntryDefinition>();
        }
        else
        {
            entriesToRun = allEntry.TestEntryDefinitionMap.Values.Where(i => idsToRun.Any(j => j.Equals(i.Id)));
        }

        await RunEntry(allEntry, pipeline, entriesToRun, pipelinePreSetup);
    }

    private async Task BuildAppEntries(AllEntry allEntry, PipelineRun pipeline, IEnumerable<string> idsToRun)
    {
        var pipelinePreSetup = await pipeline.Pipeline.GetPipelinePreSetup();

        IEnumerable<IRunEntryDefinition> entriesToRun;

        if (!idsToRun.Any())
        {
            entriesToRun = allEntry.BuildEntryDefinitionMap.Values.Cast<IRunEntryDefinition>();
        }
        else
        {
            entriesToRun = allEntry.BuildEntryDefinitionMap.Values.Where(i => idsToRun.Any(j => j.Equals(i.Id)));
        }

        await RunEntry(allEntry, pipeline, entriesToRun, pipelinePreSetup);
    }

    private async Task PublishAppEntries(AllEntry allEntry, PipelineRun pipeline, IEnumerable<string> idsToRun)
    {
        var pipelinePreSetup = await pipeline.Pipeline.GetPipelinePreSetup();

        IEnumerable<IRunEntryDefinition> entriesToRun;

        if (!idsToRun.Any())
        {
            entriesToRun = allEntry.PublishEntryDefinitionMap.Values.Cast<IRunEntryDefinition>();
        }
        else
        {
            entriesToRun = allEntry.PublishEntryDefinitionMap.Values.Where(i => idsToRun.Any(j => j.Equals(i.Id)));
        }

        await RunEntry(allEntry, pipeline, entriesToRun, pipelinePreSetup);
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

                var pipeline = await PipelineHelpers.SetupPipeline(this);
                var pipelinePreSetup = await pipeline.Pipeline.GetPipelinePreSetup();

                Console.WriteLine();

                await RunEntry(allEntry, pipeline, [runEntryDefinition], pipelinePreSetup);
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

    private static int CompareBuildIds(string? a, string? b)
    {
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 0;
        if (string.IsNullOrEmpty(a)) return -1;
        if (string.IsNullOrEmpty(b)) return 1;

        // Remove "build." prefix if present
        string StripPrefix(string s) => s.StartsWith("build.", StringComparison.OrdinalIgnoreCase) ? s[6..] : s;

        string sa = StripPrefix(a);
        string sb = StripPrefix(b);

        // Try to parse as new format: "yyyyMMddHHmmss.<hash>"
        bool IsNewFormat(string s) => s.Length >= 15 &&
            long.TryParse(s.AsSpan(0, 14), out _) &&
            s[14] == '.' && s.Length > 15;

        bool aNew = IsNewFormat(sa);
        bool bNew = IsNewFormat(sb);

        if (aNew && bNew)
        {
            // Compare by date+time, then by hash
            int dateCmp = string.Compare(sa[..14], sb[..14], StringComparison.Ordinal);
            if (dateCmp != 0) return dateCmp;
            return string.Compare(sa, sb, StringComparison.Ordinal);
        }
        if (!aNew && !bNew)
        {
            // Both are old format, try to parse as int
            if (int.TryParse(sa, out int ai) && int.TryParse(sb, out int bi))
                return ai.CompareTo(bi);
            return string.Compare(sa, sb, StringComparison.Ordinal);
        }
        // If one is new and one is old, always treat new as greater
        return aNew ? 1 : -1;
    }

    internal async Task RunPipelinePreSetup()
    {
        CheckEnvironementBranches();

        var allEntry = await ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this));

        CheckAppEntry(allEntry);

        EntryHelpers.SetupSecretVariables(this);

        var pipeline =  await PipelineHelpers.SetupPipeline(this);

        await pipeline.Pipeline.PreparePreSetup(allEntry);

        Log.Information("Target branch: {branch}", pipeline.PipelineInfo.Branch);
        Log.Information("Trigger type: {branch}", pipeline.PipelineInfo.TriggerType);

        PipelineType = pipeline.PipelineType;

        string env = pipeline.PipelineInfo.Branch.ToLowerInvariant();

        ObjectHolder<IReadOnlyCollection<Output>> lsRemote = new();

        Dictionary<string, AppRunEntry> toEntry = [];

        string buildIdStr = GitTasks.Git("show -s --date=format:%Y%m%d%H%M%S --format=%cd.%h --abbrev=7 HEAD").FirstOrDefault().Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(buildIdStr))
            throw new Exception("Failed to get build id from git.");

        string targetBuildId = "";

        bool useVersionFile = await allEntry.WorkflowConfigEntryDefinition.GetUseJsonFileVersioning();

        bool isFirstRelease = true;

        foreach (var key in allEntry.AppEntryMap.Select(i => i.Key))
        {
            string appId = key;

            ValueHelpers.GetOrFail(appId, allEntry, out var appEntry);

            var allVersions = await ValueHelpers.GetOrFail(() => EntryHelpers.GetAllVersions(this, allEntry, appId, lsRemote));

            if (useVersionFile && pipeline.PipelineInfo.TriggerType != TriggerType.Tag)
            {
                EntryHelpers.VerifyVersionsFile(allVersions, appId, [pipeline.PipelineInfo.Branch]);
            }

            bool hasBumped = false;
            SemVersion? lastVersionGroup = null;
            SemVersion? currentLatest = null;

            if (allVersions.EnvSorted.Count != 0 && allVersions.EnvVersionGrouped.TryGetValue(env, out var versionGroup) && versionGroup.Count != 0)
            {
                lastVersionGroup = versionGroup.Last();

                if (!allVersions.EnvLatestVersionPaired.TryGetValue(env, out currentLatest) || currentLatest != lastVersionGroup)
                {
                    if (allVersions.VersionBump.Contains(lastVersionGroup) &&
                        !allVersions.VersionQueue.Contains(lastVersionGroup) &&
                        !allVersions.VersionFailed.Contains(lastVersionGroup) &&
                        !allVersions.VersionPassed.Contains(lastVersionGroup))
                    {
                        if ((useVersionFile && pipeline.PipelineInfo.TriggerType == TriggerType.Commit) || pipeline.PipelineInfo.TriggerType == TriggerType.Tag)
                        {
                            hasBumped = true;
                            Log.Information("{appId} Tag: {current}, current latest: {latest}", appId, currentLatest?.ToString(), lastVersionGroup.ToString());
                        }
                    }
                }
                else
                {
                    if ((useVersionFile && pipeline.PipelineInfo.TriggerType == TriggerType.Commit) || pipeline.PipelineInfo.TriggerType == TriggerType.Tag)
                    {
                        Log.Information("{appId} Tag: {current}, already latest", appId, lastVersionGroup.ToString());
                    }
                }

                if (hasBumped && allVersions.EnvBuildIdGrouped.TryGetValue(env, out var envBuildIdGrouped))
                {
                    foreach (var version in versionGroup.OrderByDescending(i => i, SemVersion.PrecedenceComparer))
                    {
                        if (allVersions.VersionPassed.Contains(version) &&
                            allVersions.VersionCommitPaired.TryGetValue(version, out var lastSuccessCommit) &&
                            allVersions.CommitBuildIdGrouped.TryGetValue(lastSuccessCommit, out var buildIdGroup))
                        {
                            var envBuildIdSuccessGrouped = buildIdGroup.Where(envBuildIdGrouped.Contains);
                            string? maxBuildId = null;
                            foreach (var envBuildId in envBuildIdSuccessGrouped)
                            {
                                if (maxBuildId == null || CompareBuildIds(envBuildId, maxBuildId) > 0)
                                {
                                    maxBuildId = envBuildId;
                                }
                            }
                            if (!string.IsNullOrEmpty(maxBuildId))
                            {
                                isFirstRelease = false;
                                if (string.IsNullOrEmpty(targetBuildId))
                                {
                                    targetBuildId = maxBuildId;
                                }
                                else
                                {
                                    targetBuildId = CompareBuildIds(maxBuildId, targetBuildId) < 0 ? maxBuildId : targetBuildId;
                                }
                            }
                            break;
                        }
                    }
                }
            }

            toEntry.Add(appId, new()
            {
                AppId = appEntry.AppId,
                Environment = env,
                Version = lastVersionGroup?.ToString() ?? "",
                OldVersion = currentLatest?.ToString() ?? "",
                HasRelease = hasBumped
            });
        }

        foreach (var rel in toEntry.Values.Where(i => i.HasRelease))
        {
            Log.Information("{appId} on {env} has new version {newVersion}", rel.AppId, rel.Environment, rel.Version);
        }

        var releaseNotes = "";
        var buildId = buildIdStr;
        var buildTag = "build." + buildId;
        var targetBuildTag = "build." + targetBuildId;
        var hasEntries = toEntry.Count != 0;
        var hasRelease = toEntry.Any(i => i.Value.HasRelease);

        string versionFactory(string version)
        {
            if (SemVersion.TryParse(version, SemVersionStyles.Strict, out var semVersion))
            {
                if (pipeline.PipelineInfo.TriggerType == TriggerType.PullRequest)
                {
                    return SemVersion.Parse($"{semVersion.WithoutMetadata()}+build.{buildId}-pr.{pipeline.PipelineInfo.PullRequestNumber}", SemVersionStyles.Strict).ToString();
                }
                else
                {
                    return SemVersion.Parse($"{semVersion.WithoutMetadata()}+build.{buildId}", SemVersionStyles.Strict).ToString();
                }
            }
            return "";
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

            if (!env.Equals(MainEnvironmentBranch, StringComparison.InvariantCultureIgnoreCase))
            {
                ghReleaseCreateArgs += " --prerelease";
            }

            if (!isFirstRelease)
            {
                ghReleaseCreateArgs += " --notes-start-tag " + targetBuildTag;
            }

            Gh.Invoke(ghReleaseCreateArgs, logger: (s, e) => Log.Debug(e));

            var releaseNotesJson = Gh.Invoke($"release view {buildTag} --json body", logOutput: false, logInvocation: false).FirstOrDefault().Text;
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

            int newVersionCount = 0;
            foreach (var entry in toEntry.Values.Where(i => i.HasRelease))
            {
                var appId = entry.AppId.ToLowerInvariant();
                var oldVer = entry.OldVersion;
                var newVer = SemVersion.Parse(entry.Version, SemVersionStyles.Strict).WithoutMetadata().ToString();
                if (string.IsNullOrEmpty(oldVer))
                {
                    releaseNotes += $"\n* Bump `{appId}` to `{newVer}`. See [changelog]({gitBaseUrl}/commits/{appId}/{newVer})";
                }
                else
                {
                    releaseNotes += $"\n* Bump `{appId}` from `{oldVer}` to `{newVer}`. See [changelog]({gitBaseUrl}/compare/{appId}/{oldVer}...{appId}/{newVer})";
                }
                newVersionCount++;
            }
            if (newVersionCount > 1)
            {
                releaseNotes = releaseNotes.Insert(0, "## New Apps");
            }
            else
            {
                releaseNotes = releaseNotes.Insert(0, "## New Version");
            }

            releaseNotes += "\n\n" + releaseNotesFromProp;

            releaseNotes = releaseNotes.Replace("\n\n\n**Full Changelog**", "\n\n**Full Changelog**");

            Log.Information("Generated release notes:\n{Notes}", releaseNotes);

            var notesPath = TemporaryDirectory / "notes.md";
            notesPath.WriteAllText(releaseNotes);

            string ghReleaseEditArgs = $"release edit {buildTag} " +
                $"--notes-file {notesPath}";

            Gh.Invoke(ghReleaseEditArgs, logger: (s, e) => Log.Debug(e));
        }

        foreach (var targetEntry in allEntry.TargetEntryDefinitionMap.Values)
        {
            List<(AppVersion AppVersion, RunType RunType)> appSetup = [];

            foreach (var appId in targetEntry.AppIds)
            {
                if (!toEntry.TryGetValue(ValueHelpers.GetOrNullFail(appId), out var entry))
                {
                    continue;
                }

                RunType runType = pipeline.PipelineInfo.TriggerType switch
                {
                    TriggerType.PullRequest => RunType.PullRequest,
                    TriggerType.Commit => useVersionFile ? (entry.HasRelease ? RunType.Bump : RunType.Commit) : RunType.Commit,
                    TriggerType.Tag => entry.HasRelease ? RunType.Bump : RunType.Commit,
                    TriggerType.Local => RunType.Local,
                    _ => throw new NotSupportedException()
                };

                appSetup.Add((new AppVersion
                {
                    AppId = entry.AppId.NotNullOrEmpty(),
                    Environment = entry.Environment,
                    Version = ParseSemVersion(entry),
                    BuildId = buildId
                }, runType));
            }

            SetupRunContext(targetEntry, appSetup, releaseNotes, pipeline);
        }

        Dictionary<string, EntrySetup> testEntrySetupMap = [];
        Dictionary<string, EntrySetup> buildEntrySetupMap = [];
        Dictionary<string, EntrySetup> publishEntrySetupMap = [];
        Dictionary<string, EntrySetup> targetEntrySetupMap = [];
        Dictionary<string, EntrySetup> dependentEntrySetupMap = [];
        Dictionary<string, EntrySetup> entrySetupMap = [];

        async Task<EntrySetup> createEntrySetup(IRunEntryDefinition entry)
        {
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

            var runnerOs = await entry.GetRunnerOS();
            var runnerPipelineOS = "local";
            var runScript = "local";
            if (PipelineType != Pipelines.Common.Enums.PipelineType.Local)
            {
                runnerPipelineOS = JsonSerializer.Serialize(runnerOs.GetPipelineOS(PipelineType), JsonExtension.SnakeCaseNamingOptionIndented);
                runScript = runnerOs.GetRunScript(PipelineType);
            }

            EntrySetup setup = new()
            {
                Id = entry.Id,
                RunTypes = ValueHelpers.GetOrNullFail(entry.RunContext).Apps.ToDictionary(i => i.Key, i => i.Value.RunType),
                Condition = await entry.GetCondition(),
                RunnerOSSetup = new()
                {
                    Name = runnerOs.Name,
                    RunnerPipelineOS = runnerPipelineOS,
                    RunScript = runScript
                },
                CacheInvalidator = await entry.GetCacheInvalidator(),
                CachePaths = [.. flatCachePaths],
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
            List<(AppVersion AppVersion, RunType RunType)> appSetup = [];

            foreach (var appId in dependentEntry.AppIds)
            {
                if (!toEntry.TryGetValue(ValueHelpers.GetOrNullFail(appId), out var entry))
                {
                    continue;
                }

                RunType runType = pipeline.PipelineInfo.TriggerType switch
                {
                    TriggerType.PullRequest => RunType.PullRequest,
                    TriggerType.Commit => useVersionFile ? (entry.HasRelease ? RunType.Bump : RunType.Commit) : RunType.Commit,
                    TriggerType.Tag => entry.HasRelease ? RunType.Bump : RunType.Commit,
                    TriggerType.Local => RunType.Local,
                    _ => throw new NotSupportedException()
                };

                appSetup.Add((new AppVersion
                {
                    AppId = entry.AppId.NotNullOrEmpty(),
                    Environment = entry.Environment,
                    Version = ParseSemVersion(entry),
                    BuildId = buildId
                }, runType));
            }

            SetupRunContext(dependentEntry, appSetup, releaseNotes, pipeline);
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
    }

    private static SemVersion ParseSemVersion(AppRunEntry entry)
    {
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
        return version;
    }
}
