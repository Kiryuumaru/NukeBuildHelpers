using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.Tooling;

namespace NukeBuildHelpers;
/// <summary>
/// Represents the base class for Nuke build helpers with common properties and methods.
/// </summary>
public abstract partial class BaseNukeBuildHelpers : NukeBuild, INukeBuildHelpers
{
    internal static AbsolutePath CommonCacheDirectory => RootDirectory / ".nuke" / "cache";

    /// <summary>
    /// Gets the output directory path.
    /// </summary>
    public static AbsolutePath OutputDirectory => RootDirectory / ".nuke" / "output";

    /// <summary>
    /// Gets the cache directory path.
    /// </summary>
    public static AbsolutePath CacheDirectory => CommonCacheDirectory / "output";

    /// <summary>
    /// Gets the list of environment branches.
    /// </summary>
    public virtual string[] EnvironmentBranches { get; } = [
        "alpha",
        "beta",
        "rc",
        "main",
        ];

    /// <summary>
    /// Gets the main environment branch.
    /// </summary>
    public virtual string MainEnvironmentBranch { get; } = "main";

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

    private IReadOnlyDictionary<string, string?>? splitArgs;
}
