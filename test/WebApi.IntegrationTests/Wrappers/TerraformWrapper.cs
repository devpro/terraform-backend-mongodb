using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Wrappers;

public class TerraformWrapper(string workingDirectory)
{
    private readonly string _workingDirectory = workingDirectory ?? throw new ArgumentNullException(nameof(workingDirectory));

    /// <summary>
    /// Executes a Terraform command with optional env vars and timeout.
    /// Returns output, error, and exit code for inspection.
    /// </summary>
    /// <param name="command">Terraform command (e.g., "init", "plan -out=plan.tfplan", "apply plan.tfplan")</param>
    /// <param name="environmentVariables"></param>
    /// <param name="timeout"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="TimeoutException"></exception>
    public async Task<(string StdOut, string StdErr, int ExitCode)> ExecuteAsync(
        string command,
        Dictionary<string, string?>? environmentVariables = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var env = environmentVariables ?? new Dictionary<string, string?>();
        env["TF_IN_AUTOMATION"] = "true";

        var cli = Cli.Wrap("terraform")
            .WithWorkingDirectory(_workingDirectory)
            .WithArguments(command)
            .WithEnvironmentVariables(env)
            .WithValidation(CommandResultValidation.None);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout.HasValue)
        {
            cts.CancelAfter(timeout.Value);
        }

        try
        {
            var result = await cli.ExecuteBufferedAsync(cts.Token);
            return (result.StandardOutput.Trim(), result.StandardError.Trim(), result.ExitCode);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"Terraform command '{command}' timed out after {timeout?.TotalSeconds ?? 0} seconds.");
        }
    }
}
