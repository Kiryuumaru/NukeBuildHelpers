using NukeBuildHelpers.RunContext.Interfaces;
using System.Diagnostics.CodeAnalysis;

namespace NukeBuildHelpers.RunContext.Extensions;

/// <summary>
/// Provides extension methods for handling different run contexts.
/// </summary>
public static class RunContextExtensions
{
    /// <summary>
    /// Tries to get a specific context from the provided run context.
    /// </summary>
    /// <typeparam name="TRunContext">The type of run context to get.</typeparam>
    /// <param name="runContext">The current run context.</param>
    /// <param name="context">The specific run context if found.</param>
    /// <returns>True if the specific context is found; otherwise, false.</returns>
    public static bool TryGetContext<TRunContext>(this IRunContext runContext, [NotNullWhen(true)] out TRunContext? context)
        where TRunContext : IRunContext
    {
        if (runContext is TRunContext c)
        {
            context = c;
            return true;
        }
        context = default;
        return false;
    }

    /// <summary>
    /// Tries to get a local context from the provided run context.
    /// </summary>
    /// <param name="runContext">The current run context.</param>
    /// <param name="localContext">The local context if found.</param>
    /// <returns>True if the local context is found; otherwise, false.</returns>
    public static bool TryGetLocalContext(this IRunContext runContext, [NotNullWhen(true)] out ILocalContext? localContext)
    {
        return TryGetContext(runContext, out localContext);
    }

    /// <summary>
    /// Tries to get a pipeline context from the provided run context.
    /// </summary>
    /// <param name="runContext">The current run context.</param>
    /// <param name="pipelineContext">The pipeline context if found.</param>
    /// <returns>True if the pipeline context is found; otherwise, false.</returns>
    public static bool TryGetPipelineContext(this IRunContext runContext, [NotNullWhen(true)] out IPipelineContext? pipelineContext)
    {
        return TryGetContext(runContext, out pipelineContext);
    }

    /// <summary>
    /// Tries to get a commit context from the provided run context.
    /// </summary>
    /// <param name="runContext">The current run context.</param>
    /// <param name="commitContext">The commit context if found.</param>
    /// <returns>True if the commit context is found; otherwise, false.</returns>
    public static bool TryGetCommitContext(this IRunContext runContext, [NotNullWhen(true)] out ICommitContext? commitContext)
    {
        return TryGetContext(runContext, out commitContext);
    }

    /// <summary>
    /// Tries to get a versioned context from the provided run context.
    /// </summary>
    /// <param name="runContext">The current run context.</param>
    /// <param name="versionedContext">The versioned context if found.</param>
    /// <returns>True if the versioned context is found; otherwise, false.</returns>
    public static bool TryGetVersionedContext(this IRunContext runContext, [NotNullWhen(true)] out IVersionedContext? versionedContext)
    {
        return TryGetContext(runContext, out versionedContext);
    }

    /// <summary>
    /// Tries to get a bump context from the provided run context.
    /// </summary>
    /// <param name="runContext">The current run context.</param>
    /// <param name="bumpContext">The bump context if found.</param>
    /// <returns>True if the bump context is found; otherwise, false.</returns>
    public static bool TryGetBumpContext(this IRunContext runContext, [NotNullWhen(true)] out IBumpContext? bumpContext)
    {
        return TryGetContext(runContext, out bumpContext);
    }

    /// <summary>
    /// Tries to get a pull request context from the provided run context.
    /// </summary>
    /// <param name="runContext">The current run context.</param>
    /// <param name="pullRequestContext">The pull request context if found.</param>
    /// <returns>True if the pull request context is found; otherwise, false.</returns>
    public static bool TryGetPullRequestContext(this IRunContext runContext, [NotNullWhen(true)] out IPullRequestContext? pullRequestContext)
    {
        return TryGetContext(runContext, out pullRequestContext);
    }
}
