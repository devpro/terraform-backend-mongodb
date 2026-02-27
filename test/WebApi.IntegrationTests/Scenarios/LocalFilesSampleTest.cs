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
        await ExecuteTerraformAsync("init",
            expectedOutput: "Terraform has been successfully initialized!");

        await ExecuteTerraformAsync("plan",
            expectedOutput: "Plan: 3 to add, 0 to change, 0 to destroy.");

        await ExecuteTerraformAsync("apply -auto-approve",
            expectedOutput: "Apply complete! Resources: 3 added, 0 changed, 0 destroyed.");

        // TODO: check file exist, check state commands, check idempotency

        await ExecuteTerraformAsync("destroy -auto-approve",
            expectedOutput: "Destroy complete! Resources: 3 destroyed.");
    }
}
