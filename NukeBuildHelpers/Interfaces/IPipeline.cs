using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Models;
using Octokit;
using Semver;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Interfaces;

internal interface IPipeline
{
    BaseNukeBuildHelpers NukeBuild { get; set; }

    PipelineInfo GetPipelineInfo();

    void Prepare(PreSetupOutput preSetupOutput, List<AppTestEntry> appTestEntries, Dictionary<string, (AppEntry Entry, List<AppTestEntry> Tests)> appEntryConfigs, List<(AppEntry AppEntry, string Env, SemVersion Version)> toRelease);

    void BuildWorkflow();
}
