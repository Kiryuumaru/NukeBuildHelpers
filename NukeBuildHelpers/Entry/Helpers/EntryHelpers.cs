using Nuke.Common;
using Nuke.Common.Tooling;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using NukeBuildHelpers.Common.Attributes;
using NukeBuildHelpers.Entry.Definitions;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Entry.Models;
using Semver;
using System.Reflection;

namespace NukeBuildHelpers.Entry.Helpers;

internal static class EntryHelpers
{
    internal static AllEntry GetAll(BaseNukeBuildHelpers nukeBuildHelpers)
    {
        Dictionary<string, AppEntry> appEntryMap = [];

        List<ITestEntryDefinition> testEntryDefinitions = [];
        List<IBuildEntryDefinition> buildEntryDefinitions = [];
        List<IPublishEntryDefinition> publishEntryDefinitions = [];

        foreach (var property in nukeBuildHelpers.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (property.PropertyType == typeof(TestEntry))
            {
                var testEntry = (TestEntry)property.GetValue(nukeBuildHelpers)!;
                testEntryDefinitions.Add(testEntry.Invoke(new TestEntryDefinition() { Id = property.Name }));
            }
            else if (property.PropertyType == typeof(BuildEntry))
            {
                var buildEntry = (BuildEntry)property.GetValue(nukeBuildHelpers)!;
                buildEntryDefinitions.Add(buildEntry.Invoke(new BuildEntryDefinition() { Id = property.Name }));
            }
            else if (property.PropertyType == typeof(PublishEntry))
            {
                var publishEntry = (PublishEntry)property.GetValue(nukeBuildHelpers)!;
                publishEntryDefinitions.Add(publishEntry.Invoke(new PublishEntryDefinition() { Id = property.Name }));
            }
        }

        List<IEntryDefinition> entryDefinitions = [];
        List<ITargetEntryDefinition> targetEntryDefinitions = [];
        List<IDependentEntryDefinition> dependentEntryDefinitions = [];

        entryDefinitions.AddRange(testEntryDefinitions);
        entryDefinitions.AddRange(buildEntryDefinitions);
        entryDefinitions.AddRange(publishEntryDefinitions);
        targetEntryDefinitions.AddRange(buildEntryDefinitions);
        targetEntryDefinitions.AddRange(publishEntryDefinitions);
        dependentEntryDefinitions.AddRange(testEntryDefinitions);

        foreach (var testEntryDefinition in testEntryDefinitions)
        {
            testEntryDefinition.RunnerOS.NotNull($"RunnerOS for {testEntryDefinition.Id} is null");
            foreach (var appId in testEntryDefinition.AppIds)
            {
                var appIdLower = appId.ToLowerInvariant();
                if (!appEntryMap.TryGetValue(appIdLower, out var appEntry))
                {
                    appEntry = new()
                    {
                        AppId = appIdLower
                    };
                    appEntryMap.Add(appIdLower, appEntry);
                }
                appEntry.TestEntryDefinitions.Add(testEntryDefinition);
            }
        }

        foreach (var buildEntryDefinition in buildEntryDefinitions)
        {
            buildEntryDefinition.RunnerOS.NotNull($"RunnerOS for {buildEntryDefinition.Id} is null");
            string appIdLower = buildEntryDefinition.AppId.NotNullOrEmpty().ToLowerInvariant();
            if (!appEntryMap.TryGetValue(appIdLower, out var appEntry))
            {
                appEntry = new()
                {
                    AppId = appIdLower
                };
                appEntryMap.Add(appIdLower, appEntry);
            }
            appEntry.BuildEntryDefinitions.Add(buildEntryDefinition);
        }

        foreach (var publishEntryDefinition in publishEntryDefinitions)
        {
            publishEntryDefinition.RunnerOS.NotNull($"RunnerOS for {publishEntryDefinition.Id} is null");
            string appIdLower = publishEntryDefinition.AppId.NotNullOrEmpty().ToLowerInvariant();
            if (!appEntryMap.TryGetValue(appIdLower, out var appEntry))
            {
                appEntry = new()
                {
                    AppId = appIdLower
                };
                appEntryMap.Add(appIdLower, appEntry);
            }
            appEntry.PublishEntryDefinitions.Add(publishEntryDefinition);
        }

        return new()
        {
            AppEntryMap = appEntryMap,
            TestEntryDefinitionMap = testEntryDefinitions.ToDictionary(i => i.Id),
            BuildEntryDefinitionMap = buildEntryDefinitions.ToDictionary(i => i.Id),
            PublishEntryDefinitionMap = publishEntryDefinitions.ToDictionary(i => i.Id),
            EntryDefinitionMap = entryDefinitions.ToDictionary(i => i.Id),
            TargetEntryDefinitionMap = targetEntryDefinitions.ToDictionary(i => i.Id),
            DependentEntryDefinitionMap = dependentEntryDefinitions.ToDictionary(i => i.Id),
        };
    }

    internal static AllVersions GetAllVersions(BaseNukeBuildHelpers nukeBuildHelpers, string appId, ref IReadOnlyCollection<Output>? lsRemoteOutput)
    {
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
                if (rawTag.StartsWith(appId, StringComparison.InvariantCultureIgnoreCase))
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
                        versionTag = tag[..tag.LastIndexOf("-bump")];
                        isBumpVersion = true;
                    }
                    if (tag.EndsWith("-queue", StringComparison.InvariantCultureIgnoreCase))
                    {
                        versionTag = tag[..tag.LastIndexOf("-queue")];
                        isQueueVersion = true;
                    }
                    if (tag.EndsWith("-failed", StringComparison.InvariantCultureIgnoreCase))
                    {
                        versionTag = tag[..tag.LastIndexOf("-failed")];
                        isFailedVersion = true;
                    }
                    if (tag.EndsWith("-passed", StringComparison.InvariantCultureIgnoreCase))
                    {
                        versionTag = tag[..tag.LastIndexOf("-passed")];
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

    internal static List<(MemberInfo MemberInfo, SecretVariableAttribute Secret)> GetSecretVariables(BaseNukeBuildHelpers baseNukeBuildHelpers)
    {
        var nukeBuildType = baseNukeBuildHelpers.GetType();
        List<(MemberInfo MemberInfo, SecretVariableAttribute Secret)> secretMemberList = [];
        foreach (PropertyInfo prop in nukeBuildType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            foreach (object attr in prop.GetCustomAttributes(true))
            {
                if (attr is SecretVariableAttribute SecretAttr)
                {
                    secretMemberList.Add(((MemberInfo MemberInfo, SecretVariableAttribute))(prop, SecretAttr));
                }
            }
        }
        foreach (FieldInfo field in nukeBuildType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            foreach (object attr in field.GetCustomAttributes(true))
            {
                if (attr is SecretVariableAttribute SecretAttr)
                {
                    secretMemberList.Add(((MemberInfo MemberInfo, SecretVariableAttribute))(field, SecretAttr));
                }
            }
        }
        return secretMemberList;
    }

    internal static void SetupSecretVariables(BaseNukeBuildHelpers baseNukeBuildHelpers)
    {
        foreach (var (MemberInfo, Secret) in GetSecretVariables(baseNukeBuildHelpers))
        {
            var envVarName = string.IsNullOrEmpty(Secret.EnvironmentVariableName) ? "NUKE_" + Secret.SecretVariableName : Secret.EnvironmentVariableName;
            var secretValue = Environment.GetEnvironmentVariable(envVarName);
            MemberInfo.SetValue(baseNukeBuildHelpers, secretValue);
        }
    }
}
