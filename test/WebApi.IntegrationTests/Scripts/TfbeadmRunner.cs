using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Scripts;

/// <summary>
/// Runs <c>scripts/tfbeadm</c> against the suite's own database, shared by every test that exercises the
/// script rather than duplicated per test class.
/// </summary>
internal static class TfbeadmRunner
{
    /// <summary>
    /// Runs the script, with the password supplied on standard input rather than as an argument, since an
    /// argument is visible in <c>ps</c> to every user on the host and lands in the shell history.
    /// </summary>
    public static async Task<BufferedCommandResult> RunAsync(string[] arguments, string? password,
        CancellationToken cancellationToken)
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

        if (!File.Exists(Path.Combine(repositoryRoot, "scripts/tfbeadm")))
        {
            Assert.Skip("scripts/tfbeadm was not found next to the test assembly");
        }

        return await Cli.Wrap("bash")
            .WithWorkingDirectory(repositoryRoot)
            .WithArguments(["scripts/tfbeadm", .. arguments])
            .WithEnvironmentVariables(new Dictionary<string, string?>
            {
                ["MONGODB_URI"] = $"{IntegrationTestDatabase.ConnectionString}/{IntegrationTestDatabase.Name}"
            })
            .WithStandardInputPipe(password is null ? PipeSource.Null : PipeSource.FromString(password))
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync(cancellationToken);
    }
}
