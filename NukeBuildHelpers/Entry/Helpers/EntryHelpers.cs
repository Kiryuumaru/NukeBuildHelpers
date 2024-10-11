using ICSharpCode.SharpZipLib.Zip;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Common.Attributes;
using NukeBuildHelpers.Common.Models;
using NukeBuildHelpers.Entry.Definitions;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Entry.Models;
using Octokit;
using Semver;
using System.Reflection;
using System.Text.Json;

namespace NukeBuildHelpers.Entry.Helpers;

internal static class EntryHelpers
{
    internal static async Task<AllEntry> GetAll(BaseNukeBuildHelpers nukeBuildHelpers)
    {
        Dictionary<string, AppEntry> appEntryMap = [];

        IWorkflowConfigEntryDefinition? workflowConfigEntryDefinition = null;
        List<(IRunEntryDefinition Definition, string PropertyName)> definedEntryDefinitions = [];

        foreach (var property in nukeBuildHelpers.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (property.PropertyType == typeof(TestEntry))
            {
                var testEntry = (TestEntry)property.GetValue(nukeBuildHelpers)!;
                definedEntryDefinitions.Add((testEntry.Invoke(new TestEntryDefinition()), property.Name));
            }
            else if (property.PropertyType == typeof(BuildEntry))
            {
                var buildEntry = (BuildEntry)property.GetValue(nukeBuildHelpers)!;
                definedEntryDefinitions.Add((buildEntry.Invoke(new BuildEntryDefinition()), property.Name));
            }
            else if (property.PropertyType == typeof(PublishEntry))
            {
                var publishEntry = (PublishEntry)property.GetValue(nukeBuildHelpers)!;
                definedEntryDefinitions.Add((publishEntry.Invoke(new PublishEntryDefinition()), property.Name));
            }
            else if (property.PropertyType == typeof(WorkflowConfigEntry))
            {
                if (property.Name == nameof(nukeBuildHelpers.WorkflowConfig))
                {
                    var workflowConfigEntry = (WorkflowConfigEntry)property.GetValue(nukeBuildHelpers)!;
                    workflowConfigEntryDefinition = workflowConfigEntry.Invoke(new WorkflowConfigEntryDefinition());
                }
            }
        }

        if (workflowConfigEntryDefinition == null)
        {
            throw new Exception("WorkflowConfigEntry is not defined");
        }

        List<IRunEntryDefinition> entryDefinitions = [];

        async Task append(IRunEntryDefinition definition, string defaultId)
        {
            if (definition.Matrix.Count == 0)
            {
                if (definition.Id.Equals("_"))
                {
                    definition.Id = defaultId;
                }
                entryDefinitions.Add(definition);
            }
            else
            {
                int index = 0;
                foreach (var mat in definition.Matrix)
                {
                    var definitionClone = definition.Clone();
                    definitionClone.Matrix = [];
                    foreach (var createdDefinition in await mat(definitionClone))
                    {
                        index++;
                        await append(createdDefinition, defaultId + "_" + index.ToString());
                    }
                }
            }
        }

        foreach (var (Definition, PropertyName) in definedEntryDefinitions)
        {
            await append(Definition, PropertyName);
        }

        List<IDependentEntryDefinition> dependentEntryDefinitions = [];
        List<ITargetEntryDefinition> targetEntryDefinitions = [];
        List<ITestEntryDefinition> testEntryDefinitions = [];
        List<IBuildEntryDefinition> buildEntryDefinitions = [];
        List<IPublishEntryDefinition> publishEntryDefinitions = [];

        foreach (var definition in entryDefinitions)
        {
            if (definition is IDependentEntryDefinition dependentEntryDefinition)
            {
                dependentEntryDefinitions.Add(dependentEntryDefinition);
            }
            if (definition is ITargetEntryDefinition targetEntryDefinition)
            {
                targetEntryDefinitions.Add(targetEntryDefinition);
            }
            if (definition is ITestEntryDefinition testEntryDefinition)
            {
                testEntryDefinitions.Add(testEntryDefinition);
            }
            if (definition is IBuildEntryDefinition buildEntryDefinition)
            {
                buildEntryDefinitions.Add(buildEntryDefinition);
            }
            if (definition is IPublishEntryDefinition publishEntryDefinition)
            {
                publishEntryDefinitions.Add(publishEntryDefinition);
            }
        }

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
            WorkflowConfigEntryDefinition = workflowConfigEntryDefinition,
            TestEntryDefinitionMap = testEntryDefinitions.ToDictionary(i => i.Id),
            BuildEntryDefinitionMap = buildEntryDefinitions.ToDictionary(i => i.Id),
            PublishEntryDefinitionMap = publishEntryDefinitions.ToDictionary(i => i.Id),
            RunEntryDefinitionMap = entryDefinitions.ToDictionary(i => i.Id),
            TargetEntryDefinitionMap = targetEntryDefinitions.ToDictionary(i => i.Id),
            DependentEntryDefinitionMap = dependentEntryDefinitions.ToDictionary(i => i.Id),
        };
    }

    internal static async Task<AllVersions> GetAllVersions(BaseNukeBuildHelpers nukeBuildHelpers, AllEntry allEntry, string appId, ObjectHolder<IReadOnlyCollection<Output>>? lsRemoteOutputHolder)
    {
        string basePeel = "refs/tags/";

        var headCommit = nukeBuildHelpers.Repository.Commit;

        IReadOnlyCollection<Output> lsRemoteOutput = lsRemoteOutputHolder?.Value ?? nukeBuildHelpers.Git.Invoke("ls-remote -t -q", logOutput: false, logInvocation: false);

        if (lsRemoteOutputHolder != null)
        {
            lsRemoteOutputHolder.Value = lsRemoteOutput;
        }

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

        Dictionary<string, SemVersion> envVersionFileMap = [];
        try
        {
            var versionFile = GetVersionsFile(allEntry, appId);
            envVersionFileMap = versionFile.EnvironmentVersions;
        }
        catch { }

        if (await allEntry.WorkflowConfigEntryDefinition.GetUseJsonFileVersioning() && envVersionFileMap.Count > 0)
        {
            foreach (var env in nukeBuildHelpers.EnvironmentBranches)
            {
                var envLower = env.ToLowerInvariant();
                var envVersionFile = envVersionFileMap[envLower];
                if (!allVersionList.Any(i => i == envVersionFile))
                {
                    if (!commitVersionGrouped.TryGetValue(headCommit, out var pairedVersion))
                    {
                        pairedVersion = [];
                        commitVersionGrouped.Add(headCommit, pairedVersion);
                    }
                    pairedVersion.Add(envVersionFile);
                    versionBump.Add(envVersionFile);
                    allVersionList.Add(envVersionFile);
                    envVersionGrouped[envLower].Add(envVersionFile);
                }
            }
        }

        foreach (var env in nukeBuildHelpers.EnvironmentBranches)
        {
            var envLower = env.ToLowerInvariant();
            var allVersion = envVersionGrouped[envLower];
            allVersion.Sort(SemVersion.PrecedenceComparer);
        }

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
            EnvVersionFileMap = envVersionFileMap
        };
    }

    internal static AllVersionsFile GetAllVersionsFile(AllEntry allEntry)
    {
        Dictionary<string, Dictionary<string, string>>? versionsMap = null;

        try
        {
            versionsMap = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>((NukeBuild.RootDirectory / "versions.json").ReadAllText());
        }
        catch { }

        if (versionsMap == null)
        {
            throw new Exception($"{NukeBuild.RootDirectory / "versions.json"} is empty");
        }

        Dictionary<string, VersionFile> versions = [];

        foreach (var appVersion in versionsMap)
        {
            string appId = appVersion.Key;

            if (!allEntry.AppEntryMap.TryGetValue(appId, out _))
            {
                throw new Exception($"\"{appId}\" was defined in versions.json but appId does not exists");
            }

            Dictionary<string, SemVersion> envVersionMap = [];

            foreach (var envVersion in appVersion.Value)
            {
                envVersionMap[envVersion.Key] = SemVersion.Parse(envVersion.Value, SemVersionStyles.Strict);
            }

            versions[appId] = new()
            {
                AppId = appId,
                EnvironmentVersions = envVersionMap
            };
        }

        return new() { Versions = versions };
    }

    internal static VersionFile GetVersionsFile(AllEntry allEntry, string appId)
    {
        Dictionary<string, Dictionary<string, string>>? versionsMap = null;

        try
        {
            versionsMap = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>((NukeBuild.RootDirectory / "versions.json").ReadAllText());
        }
        catch { }

        if (versionsMap == null)
        {
            throw new Exception($"{NukeBuild.RootDirectory / "versions.json"} is empty");
        }

        if (!versionsMap.TryGetValue(appId.ToLowerInvariant(), out var appVersion))
        {
            throw new Exception($"\"{appId}\" does not exists in {NukeBuild.RootDirectory / "versions.json"}");
        }

        if (!allEntry.AppEntryMap.TryGetValue(appId, out _))
        {
            throw new Exception($"\"{appId}\" was defined in {NukeBuild.RootDirectory / "versions.json"} but appId does not exists");
        }

        Dictionary<string, SemVersion> envVersionMap = [];

        foreach (var envVersion in appVersion)
        {
            envVersionMap[envVersion.Key] = SemVersion.Parse(envVersion.Value, SemVersionStyles.Strict).WithoutMetadata();
        }

        return new()
        {
            AppId = appId,
            EnvironmentVersions = envVersionMap
        };
    }

    internal static async Task GenerateAllVersionsFile(BaseNukeBuildHelpers baseNukeBuildHelpers, AllEntry allEntry)
    {
        Dictionary<string, Dictionary<string, string>> versions = [];

        ObjectHolder<IReadOnlyCollection<Output>> lsRemote = new();

        foreach (var appEntry in allEntry.AppEntryMap)
        {
            string appId = appEntry.Key;

            var allVersions = await ValueHelpers.GetOrFail(() => GetAllVersions(baseNukeBuildHelpers, allEntry, appId, lsRemote));

            Dictionary<string, string> envVersions = [];

            foreach (var env in allVersions.EnvSorted)
            {
                envVersions[env] = allVersions.EnvVersionGrouped[env].Last().WithoutMetadata().ToString();
            }

            versions[appId.ToLowerInvariant()] = envVersions;
        }

        (NukeBuild.RootDirectory / "versions.json").WriteAllText(JsonSerializer.Serialize(versions, JsonExtension.OptionIndented));
    }

    internal static void VerifyVersionsFile(AllVersions allVersions, string appId, string[] envs)
    {
        foreach (var env in envs)
        {
            var envLower = env.ToLowerInvariant();
            var envVersionFile = allVersions.EnvVersionFileMap[envLower];
            var envVersions = allVersions.EnvVersionGrouped[envLower];
            var latestEnvVersion = envVersions.Last();
            if (latestEnvVersion.ComparePrecedenceTo(envVersionFile) > 0)
            {
                throw new Exception($"{NukeBuild.RootDirectory / "versions.json"} is invalid. \"{appId}\" {envLower} provided version is behind the actual latest version. {latestEnvVersion} > {envVersionFile}");
            }
        }
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
