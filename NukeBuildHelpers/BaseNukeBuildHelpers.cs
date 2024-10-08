﻿using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using NukeBuildHelpers.Entry;
using NukeBuildHelpers.Pipelines.Common.Enums;

namespace NukeBuildHelpers;

/// <summary>
/// Represents the base class for Nuke build helpers with common properties and methods.
/// </summary>
public abstract partial class BaseNukeBuildHelpers : NukeBuild, INukeBuildHelpers
{
    internal static AbsolutePath NukeDirectory { get; } = RootDirectory / ".nuke";

    internal static AbsolutePath CommonOutputDirectory { get; } = TemporaryDirectory / "output";

    internal static AbsolutePath CommonArtifactsDirectory { get; } = TemporaryDirectory / "artifacts";

    internal static AbsolutePath CommonArtifactsDownloadDirectory { get; } = TemporaryDirectory / "artifacts-download";

    internal static AbsolutePath CommonArtifactsUploadDirectory { get; } = TemporaryDirectory / "artifacts-upload";

    internal static AbsolutePath CommonCacheDirectory { get; } = TemporaryDirectory / "cache";

    internal static AbsolutePath CommonCacheOutputDirectory { get; } = CommonCacheDirectory / "output";

    /// <summary>
    /// Gets the output directory path.
    /// </summary>
    public static AbsolutePath OutputDirectory => CommonOutputDirectory / "runtime";

    /// <summary>
    /// Gets the list of environment branches.
    /// </summary>
    public abstract string[] EnvironmentBranches { get; }

    /// <summary>
    /// Gets the main environment branch.
    /// </summary>
    public abstract string MainEnvironmentBranch { get; }

    /// <inheritdoc cref="INukeBuildHelpers.Repository"/>
    protected internal GitRepository Repository => (this as INukeBuildHelpers).Repository;

    /// <inheritdoc cref="INukeBuildHelpers.Args"/>
    protected internal string Args => (this as INukeBuildHelpers).Args;

    /// <inheritdoc cref="INukeBuildHelpers.Git"/>
    protected internal Tool Git => (this as INukeBuildHelpers).Git;

    /// <inheritdoc cref="INukeBuildHelpers.Gh"/>
    protected internal Tool Gh => (this as INukeBuildHelpers).Gh;

    /// <summary>
    /// Gets the parsed arguments as a read-only dictionary.
    /// </summary>
    public IReadOnlyDictionary<string, string?> SplitArgs
    {
        get
        {
            if (splitArgs == null)
            {
                Dictionary<string, string?> targetParams = [];
                if ((this as INukeBuildHelpers).Args != null)
                {
                    foreach (var targetParam in (this as INukeBuildHelpers).Args.Split(';'))
                    {
                        if (string.IsNullOrEmpty(targetParam))
                        {
                            continue;
                        }
                        try
                        {
                            var split = targetParam.Split('=');
                            targetParams.Add(split[0], split[1]);
                        }
                        catch
                        {
                            targetParams.Add(targetParam, null);
                        }
                    }
                }

                splitArgs = targetParams;
            }

            return splitArgs;
        }
    }

    /// <summary>
    /// Gets the type of pipeline running.
    /// </summary>
    public PipelineType PipelineType { get; internal set; }

    private IReadOnlyDictionary<string, string?>? splitArgs;

    /// <summary>
    /// The overridable workflow config entry used for generating workflows.
    /// </summary>
    protected internal virtual WorkflowConfigEntry WorkflowConfig => _ => _;
}
