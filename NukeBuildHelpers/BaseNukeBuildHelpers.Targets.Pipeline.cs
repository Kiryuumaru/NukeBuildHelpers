using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Utilities.Collections;
using NukeBuildHelpers.Common;
using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Common.Models;
using NukeBuildHelpers.Entry.Helpers;
using NukeBuildHelpers.Entry.Models;
using NukeBuildHelpers.Pipelines.Azure;
using NukeBuildHelpers.Pipelines.Common;
using NukeBuildHelpers.Pipelines.Common.Interfaces;
using NukeBuildHelpers.Pipelines.Common.Models;
using NukeBuildHelpers.Pipelines.Github;
using Semver;
using Serilog;
using System.Text.Json;

namespace NukeBuildHelpers;

partial class BaseNukeBuildHelpers
{
    public Target PipelineTest => _ => _
        .Unlisted()
        .Description("To be used by pipeline")
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            ValueHelpers.GetOrFail(() => SplitArgs, out var splitArgs);
            ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this), out var allEntry);

            await TestAppEntries(allEntry, splitArgs.Select(i => i.Key));
        });

    public Target PipelineBuild => _ => _
        .Unlisted()
        .Description("To be used by pipeline")
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            ValueHelpers.GetOrFail(() => SplitArgs, out var splitArgs);
            ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this), out var allEntry);

            await BuildAppEntries(allEntry, splitArgs.Select(i => i.Key));
        });

    public Target PipelinePublish => _ => _
        .Unlisted()
        .Description("To be used by pipeline")
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            ValueHelpers.GetOrFail(() => SplitArgs, out var splitArgs);
            ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this), out var allEntry);

            await PublishAppEntries(allEntry, splitArgs.Select(i => i.Key));
        });

    public Target PipelinePreSetup => _ => _
        .Unlisted()
        .Description("To be used by pipeline")
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this), out var allEntry);

            await StartPreSetup(allEntry);
        });

    public Target PipelinePostSetup => _ => _
        .Unlisted()
        .Description("To be used by pipeline")
        .Executes(async () =>
        {
            CheckEnvironementBranches();

            ValueHelpers.GetOrFail(() => SplitArgs, out var splitArgs);
            ValueHelpers.GetOrFail(() => EntryHelpers.GetAll(this), out var allEntry);

            await StartPostSetup(allEntry);
        });
}
