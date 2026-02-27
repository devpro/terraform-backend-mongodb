using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Wrappers;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Scenarios;

public abstract class ScenarioBase: IClassFixture<KestrelWebAppFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    private readonly ITestOutputHelper _testOutputHelper;

    private string _localDirectory;

    private string _sampleFilesSourcePath;

    private readonly TerraformWrapper _terraformWrapper;

    protected ScenarioBase(KestrelWebAppFactory<Program> factory, ITestOutputHelper testOutputHelper)
    {
        _factory = factory;
        _testOutputHelper = testOutputHelper;
        _testOutputHelper.WriteLine("Current directory {0}", AppContext.BaseDirectory);
        _sampleFilesSourcePath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../../samples/local-files"));
        _localDirectory = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid().ToString()}");
        _terraformWrapper = new TerraformWrapper(_localDirectory);
    }

    public ValueTask InitializeAsync()
    {
        _testOutputHelper.WriteLine("Creating directory {0}", _localDirectory);
        CopyDirectory(_sampleFilesSourcePath, _localDirectory, overwrite: true);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(_localDirectory))
        {
            try
            {
                Directory.Delete(_localDirectory, recursive: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Temp folder cleanup failed: {ex.Message}");
            }
        }

        GC.SuppressFinalize(this);

        return ValueTask.CompletedTask;
    }

    private static void CopyDirectory(string sourceDir, string destinationDir, bool overwrite = false)
    {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists) throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

        Directory.CreateDirectory(destinationDir);

        foreach (var file in dir.GetFiles())
        {
            file.CopyTo(Path.Combine(destinationDir, file.Name), overwrite);
        }

        foreach (var subDir in dir.GetDirectories())
        {
            CopyDirectory(subDir.FullName, Path.Combine(destinationDir, subDir.Name), overwrite);
        }
    }

    protected async Task<(string StdOut, string StdErr, int ExitCode)> ExecuteTerraformAsync(
        string command,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient();
        var baseAddress = client.BaseAddress;

        var environmentVariables = new Dictionary<string, string?>
        {
            ["TF_HTTP_ADDRESS"] = $"{baseAddress}/dummy/state/local-files",
            ["TF_HTTP_LOCK_ADDRESS"] = $"{baseAddress}/dummy/state/local-files/lock",
            ["TF_HTTP_UNLOCK_ADDRESS"] = $"{baseAddress}/dummy/state/local-files/lock",
            ["TF_HTTP_USERNAME"] = "admin",
            ["TF_HTTP_PASSWORD"] = "admin123"
        };

        return await _terraformWrapper.ExecuteAsync(command, environmentVariables, timeout, cancellationToken);
    }
}
