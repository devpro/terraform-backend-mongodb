using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using CliWrap;
using CliWrap.Buffered;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Scenarios;

public abstract class ScenarioBase(TestKestrelWebAppFactory factory, ITestOutputHelper testOutputHelper)
    : IClassFixture<TestKestrelWebAppFactory>, IAsyncLifetime
{
    private readonly string _runId = Guid.NewGuid().ToString();

    /// <summary>
    /// Exposed so a scenario can reach the same database the running instance is wired to,
    /// without capturing the constructor parameter itself and duplicating what this base already holds.
    /// </summary>
    protected TestKestrelWebAppFactory Factory { get; } = factory;

    private string LocalDirectory { get { return Path.Combine(Path.GetTempPath(), $"tfbackend-test-{_runId}"); } }

    /// <summary>
    /// The tenant, name pair a scenario writes under.
    /// <para>
    /// Protected rather than private so a scenario can query <c>tf_state</c> and <c>tf_state_history</c> directly for the same document its own <c>terraform apply</c> just wrote.
    /// That is what proves the state landed as a queryable document, rather than only that Terraform accepted it.
    /// </para>
    /// </summary>
    protected string StateName { get { return $"local-files-{_runId}"; } }

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

    public async ValueTask DisposeAsync()
    {
        await RemoveScenarioStateAsync();

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
    }

    /// <summary>
    /// Removes everything the scenario wrote to the database.
    /// <para>
    /// A completed run ends on <c>terraform destroy</c>,
    /// which destroys the infrastructure but still leaves the state document behind, holding an empty state, plus one <c>tf_state_history</c> entry per apply.
    /// Nothing in the protocol deletes them, so the scenario has to.
    /// </para>
    /// <para>
    /// This runs under <see cref="CancellationToken.None"/> rather than the test's token,
    /// which is cancelled exactly when a run times out, and that is when leftovers are most likely.
    /// </para>
    /// </summary>
    private async Task RemoveScenarioStateAsync()
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("tenant", TestCredentials.Tenant),
            Builders<BsonDocument>.Filter.Eq("name", StateName));

        try
        {
            var database = Factory.Services.GetRequiredService<IMongoDatabase>();
            foreach (var collectionName in new[] { "tf_state", "tf_state_lock", "tf_state_history" })
            {
                await database.GetCollection<BsonDocument>(collectionName)
                    .DeleteManyAsync(filter, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            testOutputHelper.WriteLine("Scenario state cleanup failed: {0}", ex.Message);
            throw;
        }
    }

    protected async Task ExecuteTerraformAsync(
        string command,
        int expectedReturnCode = 0,
        string expectedOutput = "",
        string expectedError = "",
        TimeSpan? timeout = null)
    {
        var baseAddress = Factory.ServerAddress;

        var environmentVariables = new Dictionary<string, string?>
        {
            ["TF_HTTP_ADDRESS"] = $"{baseAddress}/{TestCredentials.Tenant}/state/{StateName}",
            ["TF_HTTP_LOCK_ADDRESS"] = $"{baseAddress}/{TestCredentials.Tenant}/state/{StateName}/lock",
            ["TF_HTTP_UNLOCK_ADDRESS"] = $"{baseAddress}/{TestCredentials.Tenant}/state/{StateName}/lock",
            ["TF_HTTP_USERNAME"] = TestCredentials.Username,
            ["TF_HTTP_PASSWORD"] = TestCredentials.Password
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
