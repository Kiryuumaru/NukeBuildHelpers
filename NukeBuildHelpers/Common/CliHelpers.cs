using CliWrap;
using CliWrap.EventStream;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Common;

public static class CliHelpers
{
    public static Command BuildRun(
        string command,
        IDictionary<string, string?>? environmentVariables = default,
        PipeTarget? outPipeTarget = default,
        PipeTarget? errPipeTarget = default)
    {
        Command osCli;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            osCli = CliWrap.Cli.Wrap("cmd")
                .WithArguments(["/c", $"\"{command.Replace("\"", "\\\"")}\""], false);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            osCli = CliWrap.Cli.Wrap("/bin/bash")
                .WithArguments(["-c", $"\"{command.Replace("\"", "\\\"")}\""], false);
        }
        else
        {
            throw new NotImplementedException();
        }

        osCli = osCli
            .WithValidation(CommandResultValidation.None)
            .WithEnvironmentVariables(environmentVariables?.ToDictionary() ?? []);

        if (outPipeTarget != null)
        {
            osCli = osCli
                .WithStandardOutputPipe(outPipeTarget);
        }

        if (errPipeTarget != null)
        {
            osCli = osCli
                .WithStandardErrorPipe(errPipeTarget);
        }

        return osCli;
    }

    public static async Task RunOnce(
        string command,
        IDictionary<string, string?>? environmentVariables,
        PipeTarget? outPipeTarget,
        PipeTarget? errPipeTarget,
        CancellationToken stoppingToken = default)
    {
        await BuildRun(command, environmentVariables, outPipeTarget, errPipeTarget)
            .ExecuteAsync(stoppingToken);
    }

    public static async Task<string> RunOnce(string command, CancellationToken stoppingToken = default)
    {
        var stdBuffer = new StringBuilder();

        await BuildRun(command, null, PipeTarget.ToStringBuilder(stdBuffer), PipeTarget.ToStringBuilder(stdBuffer))
            .ExecuteAsync(stoppingToken);

        return stdBuffer.ToString();
    }

    public static async Task<string> RunOnce(
        string command,
        IDictionary<string, string?>? environmentVariables,
        CancellationToken stoppingToken = default)
    {
        var stdBuffer = new StringBuilder();

        await BuildRun(command, environmentVariables, PipeTarget.ToStringBuilder(stdBuffer), PipeTarget.ToStringBuilder(stdBuffer))
            .ExecuteAsync(stoppingToken);

        return stdBuffer.ToString();
    }

    public static IAsyncEnumerable<CommandEvent> RunListen(string command, IDictionary<string, string?>? environmentVariables = default, CancellationToken stoppingToken = default)
    {
        var osCli = BuildRun(command, environmentVariables, null);

        return osCli.ListenAsync(stoppingToken);
    }
}
