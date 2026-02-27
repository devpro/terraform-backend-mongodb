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
        // TODO: clean copy, with no local terraform state

        await ExecuteTerraformAsync("init -reconfigure",
            expectedOutput: "Terraform has been successfully initialized!");

        // TODO: smart check on output

        await ExecuteTerraformAsync("plan",
            expectedOutput: "Terraform used the selected providers to generate the following execution\nplan.");

        // TODO: apply, destroy and check after apply
    }
}
