using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Wrappers;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Scenarios;

public abstract class ScenarioBase : IClassFixture<KestrelWebAppFactory<Program>>, IAsyncLifetime
{
    private readonly KestrelWebAppFactory<Program> _factory;

    private readonly ITestOutputHelper _testOutputHelper;

    private readonly string _localDirectory;

    private readonly string _sampleFilesSourcePath;

    private readonly TerraformWrapper _terraformWrapper;

    protected ScenarioBase(KestrelWebAppFactory<Program> factory, ITestOutputHelper testOutputHelper)
    {
        _factory = factory;
        _testOutputHelper = testOutputHelper;
        _testOutputHelper.WriteLine("Executing from {0}", AppContext.BaseDirectory);
        _sampleFilesSourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "../../../../../samples/local-files"));
        _localDirectory = Path.Combine(Path.GetTempPath(), $"tfbackend-test-{Guid.NewGuid().ToString()}");
        _terraformWrapper = new TerraformWrapper(_localDirectory);
    }

    public ValueTask InitializeAsync()
    {
        _testOutputHelper.WriteLine("Copying files from {0}", _sampleFilesSourcePath);
        CopyDirectory(_sampleFilesSourcePath, _localDirectory, "*.tf", overwrite: true);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(_localDirectory))
        {
            try
            {
                _testOutputHelper.WriteLine("Deleting directory {0}", _localDirectory);
                Directory.Delete(_localDirectory, recursive: true);
            }
            catch (Exception ex)
            {
                _testOutputHelper.WriteLine("Temp folder cleanup failed: {0}", ex.Message);
            }
        }

        GC.SuppressFinalize(this);

        return ValueTask.CompletedTask;
    }

    private void CopyDirectory(string sourceDir,
        string destinationDir,
        string filePattern = "*.*",
        bool overwrite = false)
    {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists) throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

        _testOutputHelper.WriteLine("Creating directory {0}", _localDirectory);
        Directory.CreateDirectory(destinationDir);

        foreach (var file in dir.GetFiles(filePattern))
        {
            _testOutputHelper.WriteLine("Copying file {0}", file.Name);
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

    protected async Task ExecuteTerraformAsync(
        string command,
        int expectedReturnCode = 0,
        string expectedOutput = "",
        string expectedError = "",
        TimeSpan? timeout = null)
    {
        var baseAddress = _factory.ServerAddress;

        var environmentVariables = new Dictionary<string, string?>
        {
            ["TF_HTTP_ADDRESS"] = $"{baseAddress}/dummy/state/local-files",
            ["TF_HTTP_LOCK_ADDRESS"] = $"{baseAddress}/dummy/state/local-files/lock",
            ["TF_HTTP_UNLOCK_ADDRESS"] = $"{baseAddress}/dummy/state/local-files/lock",
            ["TF_HTTP_USERNAME"] = "admin",
            ["TF_HTTP_PASSWORD"] = "admin123"
        };

        _testOutputHelper.WriteLine("Executing Terraform command {0}", command);

        var (output, err, code) = await _terraformWrapper.ExecuteAsync(
            $"{command} -no-color",
            environmentVariables,
            timeout ?? TimeSpan.FromMinutes(2),
            TestContext.Current.CancellationToken);

        if (code != 0 && expectedReturnCode == 0)
        {
            _testOutputHelper.WriteLine("Error occured {0}\n{1}\n{2}", code, output, err);
        }

        code.Should().Be(expectedReturnCode);
        output.Should().Contain(expectedOutput);
        err.Should().Be(expectedError);
    }
}
