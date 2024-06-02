using Microsoft.Extensions.DependencyModel;
using Nuke.Common.Tooling;
using NukeBuildHelpers.Models;
using Octokit;
using Semver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Common;

internal static class AppEntryHelpers
{
    internal static AppConfig GetAppConfig()
    {
        Dictionary<string, AppEntryConfig> appEntryConfigs = [];

        bool hasMainReleaseEntry = false;
        List<AppEntry> appEntries = [];
        foreach (var appEntry in ClassHelpers.GetInstances<AppEntry>())
        {
            if (!appEntry.Enable)
            {
                continue;
            }
            if (appEntry.MainRelease)
            {
                if (hasMainReleaseEntry)
                {
                    throw new Exception("Contains multiple main release app entry");
                }
                hasMainReleaseEntry = true;
            }
            appEntries.Add(appEntry);
        }

        List<(bool IsAdded, AppTestEntry AppTestEntry)> appTestEntries = [];
        foreach (var appTestEntry in ClassHelpers.GetInstances<AppTestEntry>())
        {
            if (!appTestEntry.Enable)
            {
                continue;
            }
            if (appTestEntry.AppEntryTargets == null || appTestEntry.AppEntryTargets.Length == 0)
            {
                throw new Exception($"App test entry contains null or empty app entry id \"{appTestEntry.Name}\"");
            }
            appTestEntries.Add((false, appTestEntry));
        }

        foreach (var appEntry in appEntries)
        {
            if (appEntryConfigs.ContainsKey(appEntry.Id))
            {
                throw new Exception($"Contains multiple app entry id \"{appEntry.Id}\"");
            }
            List<AppTestEntry> appTestEntriesFound = [];
            for (int i = 0; appTestEntries.Count > i; i++)
            {
                if (appTestEntries[i].AppTestEntry.AppEntryTargets.Any(i => i == appEntry.GetType()))
                {
                    appTestEntriesFound.Add(appTestEntries[i].AppTestEntry);
                    appTestEntries[i] = (true, appTestEntries[i].AppTestEntry);
                }
            }
            appEntryConfigs.Add(appEntry.Id, new() { Entry = appEntry, Tests = appTestEntriesFound });
        }

        var nonAdded = appTestEntries.Where(i => !i.IsAdded);

        if (nonAdded.Any())
        {
            foreach (var (IsAdded, AppTestEntry) in nonAdded)
            {
                foreach (var appEntryTarget in AppTestEntry.AppEntryTargets)
                {
                    if (!appEntries.Any(i => string.Equals(i.Id, appEntryTarget.Name, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        throw new Exception($"App entry id \"{appEntryTarget.Name}\" does not exist, from app test entry \"{AppTestEntry.Name}\"");
                    }
                }
            }
        }

        return new()
        {
            AppEntryConfigs = appEntryConfigs,
            AppEntries = appEntries.ToDictionary(i => i.Id),
            AppTestEntries = appTestEntries.ToDictionary(i => i.AppTestEntry.Id, i => i.AppTestEntry)
        };
    }

    internal static Dictionary<string, (Type EntryType, List<(MemberInfo MemberInfo, Attributes.SecretVariableAttribute Secret)> Secrets)> GetEntrySecretMap<T>()
        where T : Entry
    {
        var asmNames = DependencyContext.Default!.GetDefaultAssemblyNames();

        var allTypes = asmNames.Select(Assembly.Load)
            .SelectMany(t => t.GetTypes())
            .Where(p => p.GetTypeInfo().IsSubclassOf(typeof(T)) && !p.ContainsGenericParameters);

        Dictionary<string, (Type EntryType, List<(MemberInfo MemberInfo, Attributes.SecretVariableAttribute Secret)> Secrets)> entry = [];
        foreach (Type type in allTypes)
        {
            foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                foreach (object attr in prop.GetCustomAttributes(true))
                {
                    if (attr is Attributes.SecretVariableAttribute SecretAttr)
                    {
                        var id = ((T)Activator.CreateInstance(type)!).Id;
                        if (!entry.TryGetValue(id, out var secrets))
                        {
                            secrets = (type, []);
                            entry.Add(id, secrets);
                        }
                        secrets.Secrets.Add(((MemberInfo MemberInfo, Attributes.SecretVariableAttribute))(prop, SecretAttr));
                    }
                }
            }
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                foreach (object attr in field.GetCustomAttributes(true))
                {
                    if (attr is Attributes.SecretVariableAttribute SecretAttr)
                    {
                        var id = ((T)Activator.CreateInstance(type)!).Id;
                        if (!entry.TryGetValue(id, out var secrets))
                        {
                            secrets = (type, []);
                            entry.Add(id, secrets);
                        }
                        secrets.Secrets.Add(((MemberInfo MemberInfo, Attributes.SecretVariableAttribute))(field, SecretAttr));
                    }
                }
            }
        }
        return entry;
    }

    internal static AllVersions GetAllVersions(BaseNukeBuildHelpers nukeBuildHelpers, string appId, Dictionary<string, AppEntryConfig> appEntryConfigs, ref IReadOnlyCollection<Output>? lsRemoteOutput)
    {
        ValueHelpers.GetOrFail(appId, appEntryConfigs, out _, out var appEntry);

        string basePeel = "refs/tags/";
        lsRemoteOutput ??= nukeBuildHelpers.Git.Invoke("ls-remote -t -q", logOutput: false, logInvocation: false);

        Dictionary<string, List<long>> commitBuildIdGrouped = [];
        Dictionary<string, List<SemVersion>> commitVersionGrouped = [];
        Dictionary<string, List<string>> commitLatestTagGrouped = [];
        Dictionary<long, string> buildIdCommitPaired = [];
        Dictionary<SemVersion, string> versionCommitPaired = [];
        Dictionary<string, List<long>> envBuildIdGrouped = [];
        List<SemVersion> versionBump = [];
        List<SemVersion> versionQueue = [];
        List<SemVersion> versionFailed = [];
        List<SemVersion> versionPassed = [];
        foreach (var refs in lsRemoteOutput)
        {
            string rawTag = refs.Text[(refs.Text.IndexOf(basePeel) + basePeel.Length)..];
            string tag;
            string commitId = refs.Text[0..refs.Text.IndexOf(basePeel)].Trim();

            if (rawTag.StartsWith("build.", StringComparison.InvariantCultureIgnoreCase))
            {
                var buildSplit = rawTag.Split('-');
                var parsedBuildId = long.Parse(buildSplit[0].Split('.')[1]);
                if (buildSplit.Length == 1)
                {
                    if (!buildIdCommitPaired.ContainsKey(parsedBuildId))
                    {
                        buildIdCommitPaired[parsedBuildId] = commitId;
                    }
                    if (!commitBuildIdGrouped.TryGetValue(commitId, out var pairedCommitBuildId))
                    {
                        pairedCommitBuildId = [];
                        commitBuildIdGrouped.Add(commitId, pairedCommitBuildId);
                    }
                    if (!pairedCommitBuildId.Contains(parsedBuildId))
                    {
                        pairedCommitBuildId.Add(parsedBuildId);
                    }
                }
                else if (buildSplit.Length == 2)
                {
                    var buildIdEnv = buildSplit[1].ToLowerInvariant();
                    if (!nukeBuildHelpers.EnvironmentBranches.Any(i => i.Equals(buildIdEnv, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        continue;
                    }
                    if (!commitBuildIdGrouped.TryGetValue(commitId, out var pairedCommitBuildId))
                    {
                        pairedCommitBuildId = [];
                        commitBuildIdGrouped.Add(commitId, pairedCommitBuildId);
                    }
                    if (!envBuildIdGrouped.TryGetValue(buildIdEnv, out var pairedEnvBuildId))
                    {
                        pairedEnvBuildId = [];
                        envBuildIdGrouped.Add(buildIdEnv, pairedEnvBuildId);
                    }
                    if (!buildIdCommitPaired.ContainsKey(parsedBuildId))
                    {
                        buildIdCommitPaired[parsedBuildId] = commitId;
                    }
                    if (!pairedCommitBuildId.Contains(parsedBuildId))
                    {
                        pairedCommitBuildId.Add(parsedBuildId);
                    }
                    if (!pairedEnvBuildId.Contains(parsedBuildId))
                    {
                        pairedEnvBuildId.Add(parsedBuildId);
                    }
                }
            }
            else
            {
                if (appEntry.Entry.MainRelease)
                {
                    tag = rawTag;
                }
                else if (rawTag.StartsWith(appId, StringComparison.InvariantCultureIgnoreCase))
                {
                    tag = rawTag[(rawTag.IndexOf(appId, StringComparison.InvariantCultureIgnoreCase) + appId.Length + 1)..];
                }
                else
                {
                    continue;
                }
                if (tag.StartsWith("latest", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (!commitLatestTagGrouped.TryGetValue(commitId, out var pairedLatestTag))
                    {
                        pairedLatestTag = [];
                        commitLatestTagGrouped.Add(commitId, pairedLatestTag);
                    }
                    pairedLatestTag.Add(tag);
                }
                else
                {
                    string versionTag = tag;
                    bool isBumpVersion = false;
                    bool isQueueVersion = false;
                    bool isFailedVersion = false;
                    bool isPassedVersion = false;
                    if (tag.EndsWith("-bump", StringComparison.InvariantCultureIgnoreCase))
                    {
                        versionTag = tag[..(tag.LastIndexOf("-bump"))];
                        isBumpVersion = true;
                    }
                    if (tag.EndsWith("-queue", StringComparison.InvariantCultureIgnoreCase))
                    {
                        versionTag = tag[..(tag.LastIndexOf("-queue"))];
                        isQueueVersion = true;
                    }
                    if (tag.EndsWith("-failed", StringComparison.InvariantCultureIgnoreCase))
                    {
                        versionTag = tag[..(tag.LastIndexOf("-failed"))];
                        isFailedVersion = true;
                    }
                    if (tag.EndsWith("-passed", StringComparison.InvariantCultureIgnoreCase))
                    {
                        versionTag = tag[..(tag.LastIndexOf("-passed"))];
                        isPassedVersion = true;
                    }

                    if (SemVersion.TryParse(versionTag, SemVersionStyles.Strict, out SemVersion tagSemver))
                    {
                        if (!commitVersionGrouped.TryGetValue(commitId, out var pairedVersion))
                        {
                            pairedVersion = [];
                            commitVersionGrouped.Add(commitId, pairedVersion);
                        }
                        tagSemver = tagSemver.WithoutMetadata();
                        versionCommitPaired[tagSemver] = commitId;
                        if (!pairedVersion.Contains(tagSemver))
                        {
                            pairedVersion.Add(tagSemver);
                        }
                        if (isBumpVersion)
                        {
                            versionBump.Add(tagSemver);
                        }
                        if (isQueueVersion)
                        {
                            versionQueue.Add(tagSemver);
                        }
                        if (isFailedVersion)
                        {
                            versionFailed.Add(tagSemver);
                        }
                        if (isPassedVersion)
                        {
                            versionPassed.Add(tagSemver);
                        }
                    }
                }
            }
        }

        List<SemVersion> allVersionList = commitVersionGrouped.SelectMany(i => i.Value).ToList();

        foreach (var env in nukeBuildHelpers.EnvironmentBranches)
        {
            SemVersion semVersion;
            if (env.Equals(nukeBuildHelpers.MainEnvironmentBranch, StringComparison.InvariantCultureIgnoreCase))
            {
                semVersion = SemVersion.Parse($"0.0.0", SemVersionStyles.Strict);
            }
            else
            {
                semVersion = SemVersion.Parse($"0.0.0-{env.ToLowerInvariant()}.0", SemVersionStyles.Strict);
            }
            allVersionList.Add(semVersion);
        }

        List<long> allBuildIdList = commitBuildIdGrouped.SelectMany(i => i.Value).ToList();
        Dictionary<string, List<SemVersion>> envVersionGrouped = allVersionList
            .GroupBy(i => i.IsPrerelease ? i.PrereleaseIdentifiers[0].Value.ToLowerInvariant() : nukeBuildHelpers.MainEnvironmentBranch.ToLowerInvariant())
            .ToDictionary(i => i.Key, i => i.Select(j => j).ToList());

        Dictionary<string, (long BuildId, SemVersion Version)> pairedLatests = [];
        foreach (var pairedLatestTag in commitLatestTagGrouped)
        {
            string commitId = pairedLatestTag.Key;
            foreach (var latestTag in pairedLatestTag.Value)
            {
                if (!commitVersionGrouped.TryGetValue(commitId, out var versions) || versions.Count == 0)
                {
                    continue;
                }
                string env;
                var latestTagSplit = latestTag.Split('-');
                if (latestTagSplit.Length == 2)
                {
                    env = latestTagSplit[1];
                    if (!envBuildIdGrouped.TryGetValue(env, out var envBuildIds) || envBuildIds.Count == 0)
                    {
                        continue;
                    }
                    var latestVersion = versions.Where(i => i.IsPrerelease && i.PrereleaseIdentifiers[0].ToString().Equals(env, StringComparison.InvariantCultureIgnoreCase)).LastOrDefault();
                    if (latestVersion != null)
                    {
                        pairedLatests.Add(env, (envBuildIds.Max(), latestVersion));
                    }
                }
                else
                {
                    env = nukeBuildHelpers.MainEnvironmentBranch.ToLowerInvariant();
                    if (!envBuildIdGrouped.TryGetValue(env, out var envBuildIds) || envBuildIds.Count == 0)
                    {
                        continue;
                    }
                    var latestVersion = versions.Where(i => !i.IsPrerelease).LastOrDefault();
                    if (latestVersion != null)
                    {
                        pairedLatests.Add(env, (envBuildIds.Max(), latestVersion));
                    }
                }
            }
        }

        Dictionary<string, SemVersion> envLatestVersionPaired = pairedLatests.ToDictionary(i => i.Key, i => i.Value.Version);
        Dictionary<string, long> envLatestBuildIdPaired = pairedLatests.ToDictionary(i => i.Key, i => i.Value.BuildId);
        List<string> envSorted = envVersionGrouped.Select(i => i.Key).ToList();

        envSorted.Sort();
        if (envSorted.Remove(nukeBuildHelpers.MainEnvironmentBranch.ToLowerInvariant()))
        {
            envSorted.Add(nukeBuildHelpers.MainEnvironmentBranch.ToLowerInvariant());
        }
        foreach (var env in envSorted)
        {
            var allVersion = envVersionGrouped[env];
            allVersion.Sort(SemVersion.PrecedenceComparer);
        }

        return new()
        {
            CommitBuildIdGrouped = commitBuildIdGrouped,
            CommitLatestTagGrouped = commitLatestTagGrouped,
            CommitVersionGrouped = commitVersionGrouped,
            BuildIdCommitPaired = buildIdCommitPaired,
            VersionCommitPaired = versionCommitPaired,
            EnvVersionGrouped = envVersionGrouped,
            EnvBuildIdGrouped = envBuildIdGrouped,
            EnvLatestVersionPaired = envLatestVersionPaired,
            EnvLatestBuildIdPaired = envLatestBuildIdPaired,
            EnvSorted = envSorted,
            VersionBump = versionBump,
            VersionQueue = versionQueue,
            VersionFailed = versionFailed,
            VersionPassed = versionPassed,
        };
    }
}
