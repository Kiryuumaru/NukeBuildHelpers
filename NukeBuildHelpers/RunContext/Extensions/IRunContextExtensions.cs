using NukeBuildHelpers.RunContext.Interfaces;
using NukeBuildHelpers.RunContext.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.RunContext.Extensions;

public static class IRunContextExtensions
{
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

    public static bool TryGetLocalContext(this IRunContext runContext, [NotNullWhen(true)] out ILocalContext? localContext)
    {
        return TryGetContext(runContext, out localContext);
    }

    public static bool TryGetPipelineContext(this IRunContext runContext, [NotNullWhen(true)] out IPipelineContext? pipelineContext)
    {
        return TryGetContext(runContext, out pipelineContext);
    }

    public static bool TryGetCommitContext(this IRunContext runContext, [NotNullWhen(true)] out ICommitContext? commitContext)
    {
        return TryGetContext(runContext, out commitContext);
    }

    public static bool TryGetVersionedContext(this IRunContext runContext, [NotNullWhen(true)] out IVersionedContext? versionedContext)
    {
        return TryGetContext(runContext, out versionedContext);
    }

    public static bool TryGetBumpContext(this IRunContext runContext, [NotNullWhen(true)] out IBumpContext? bumpContext)
    {
        return TryGetContext(runContext, out bumpContext);
    }

    public static bool TryGetPullRequestContext(this IRunContext runContext, [NotNullWhen(true)] out IPullRequestContext? pullRequestContext)
    {
        return TryGetContext(runContext, out pullRequestContext);
    }
}
