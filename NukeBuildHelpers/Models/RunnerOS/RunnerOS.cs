using NukeBuildHelpers.Enums;
using NukeBuildHelpers.Pipelines.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Models;

public abstract class RunnerOS
{
    public static RunnerOS UbuntuLatest { get; } = new RunnerOSUbuntuLatest();

    public static RunnerOS Ubuntu2204 { get; } = new RunnerOSUbuntu2204();

    public static RunnerOS WindowsLatest { get; } = new RunnerOSWindowsLatest();

    public static RunnerOS Windows2022 { get; } = new RunnerOSWindows2022();

    public abstract RunnerPipelineOS GetPipelineOS(PipelineType pipelineType);

    public abstract string GetRunScript(PipelineType pipelineType);
}
