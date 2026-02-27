using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using CliWrap;
using CliWrap.Buffered;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Scenarios;

public abstract class ScenarioBase(KestrelWebAppFactory<Program> factory, ITestOutputHelper testOutputHelper)
    : IClassFixture<KestrelWebAppFactory<Program>>, IAsyncLifetime
{
    private readonly string _runId = Guid.NewGuid().ToString();

    private string LocalDirectory { get { return Path.Combine(Path.GetTempPath(), $"tfbackend-test-{_runId}"); } }

    protected abstract string ScenarioPath { get; }

    public ValueTask InitializeAsync()
    {
        testOutputHelper.WriteLine("Executing from {0}", AppContext.BaseDirectory);
        var sampleFilesSourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, $"../../../../../{ScenarioPath}"));
        testOutputHelper.WriteLine("Copying files from {0}", sampleFilesSourcePath);
        CopyDirectory(sampleFilesSourcePath, LocalDirectory, "*.tf", overwrite: true);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(LocalDirectory))
        {
            try
            {
                testOutputHelper.WriteLine("Deleting directory {0}", LocalDirectory);
                Directory.Delete(LocalDirectory, recursive: true);
            }
            catch (Exception ex)
            {
                testOutputHelper.WriteLine("Temp folder cleanup failed: {0}", ex.Message);
            }
        }

        GC.SuppressFinalize(this);

        return ValueTask.CompletedTask;
    }

    protected async Task ExecuteTerraformAsync(
        string command,
        int expectedReturnCode = 0,
        string expectedOutput = "",
        string expectedError = "",
        TimeSpan? timeout = null)
    {
        var baseAddress = factory.ServerAddress;

        var environmentVariables = new Dictionary<string, string?>
        {
            ["TF_HTTP_ADDRESS"] = $"{baseAddress}/dummy/state/local-files-{_runId}",
            ["TF_HTTP_LOCK_ADDRESS"] = $"{baseAddress}/dummy/state/local-files-{_runId}/lock",
            ["TF_HTTP_UNLOCK_ADDRESS"] = $"{baseAddress}/dummy/state/local-files-{_runId}/lock",
            ["TF_HTTP_USERNAME"] = "admin",
            ["TF_HTTP_PASSWORD"] = "admin123"
        };

        testOutputHelper.WriteLine("Executing Terraform command {0}", command);

        var (output, err, code) = await ExecuteAsync(
            LocalDirectory,
            $"{command} -no-color",
            environmentVariables,
            timeout ?? TimeSpan.FromMinutes(2),
            TestContext.Current.CancellationToken);

        if (code != 0 && expectedReturnCode == 0)
        {
            testOutputHelper.WriteLine("Error occured {0}\n{1}\n{2}", code, output, err);
        }

        code.Should().Be(expectedReturnCode);
        output.Should().Contain(expectedOutput);
        err.Should().Be(expectedError);
    }

    private void CopyDirectory(string sourceDir,
        string destinationDir,
        string filePattern = "*.*",
        bool overwrite = false)
    {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists) throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

        testOutputHelper.WriteLine("Creating directory {0}", LocalDirectory);
        Directory.CreateDirectory(destinationDir);

        foreach (var file in dir.GetFiles(filePattern))
        {
            testOutputHelper.WriteLine("Copying file {0}", file.Name);
            file.CopyTo(Path.Combine(destinationDir, file.Name), overwrite);
        }

        foreach (var subDir in dir.GetDirectories())
        {
            if (subDir.Name.Equals(".terraform", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            CopyDirectory(subDir.FullName,
                Path.Combine(destinationDir, subDir.Name),
                filePattern,
                overwrite);
        }
    }

    /// <summary>
    /// Executes a Terraform command with optional env vars and timeout.
    /// Returns output, error, and exit code for inspection.
    /// </summary>
    /// <param name="workingDirectory"></param>
    /// <param name="command">Terraform command (e.g., "init", "plan -out=plan.tfplan", "apply plan.tfplan")</param>
    /// <param name="environmentVariables"></param>
    /// <param name="timeout"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="TimeoutException"></exception>
    private static async Task<(string StdOut, string StdErr, int ExitCode)> ExecuteAsync(
        string workingDirectory,
        string command,
        Dictionary<string, string?>? environmentVariables = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var env = environmentVariables ?? new Dictionary<string, string?>();
        env["TF_IN_AUTOMATION"] = "true";

        var cli = Cli.Wrap("terraform")
            .WithWorkingDirectory(workingDirectory)
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
