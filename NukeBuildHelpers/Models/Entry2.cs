using Nuke.Common.IO;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Models;

namespace NukeBuildHelpers;

public abstract class Entry2<TRunContext> : BaseHelper
    where TRunContext : RunContext
{
    public virtual bool Enable { get; } = true;

    public virtual string Name { get => Id; }

    public virtual AbsolutePath[] CachePaths { get; } = [];

    public virtual string CacheInvalidator { get; } = "0";

    public abstract string Id { get; }

    public abstract RunnerOS RunnerOS { get; }

    public virtual void Run(TRunContext runContext) { }

    public virtual Task RunAsync(TRunContext runContext) { return Task.CompletedTask; }

    internal TRunContext? AppTestContext { get; set; }
}
