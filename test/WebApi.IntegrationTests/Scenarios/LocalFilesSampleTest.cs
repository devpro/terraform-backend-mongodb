using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Scenarios;

public class LocalFilesSampleTest(KestrelWebAppFactory<Program> kestrelWebAppFactory, ITestOutputHelper testOutputHelper)
    : ScenarioBase(kestrelWebAppFactory, testOutputHelper)
{
    [Fact]
    public async Task TerraformInit_Succeeds()
    {
        var (outp, err, code) = await ExecuteTerraformAsync("init -no-color",
            TimeSpan.FromMinutes(2),
            TestContext.Current.CancellationToken);

        code.Should().Be(0);
        outp.Should().Contain("Terraform has been successfully initialized!");
        err.Should().BeEmpty();
    }
}
