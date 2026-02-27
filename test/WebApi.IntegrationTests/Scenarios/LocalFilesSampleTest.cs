using System.Threading.Tasks;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Scenarios;

public class LocalFilesSampleTest(KestrelWebAppFactory<Program> kestrelWebAppFactory, ITestOutputHelper testOutputHelper)
    : ScenarioBase(kestrelWebAppFactory, testOutputHelper)
{
    protected override string ScenarioPath => "samples/local-files";

    [Fact]
    public async Task TerraformInit_Succeeds()
    {
        await ExecuteTerraformAsync("init",
            expectedOutput: "Terraform has been successfully initialized!");

        await ExecuteTerraformAsync("plan",
            expectedOutput: "Plan: 3 to add, 0 to change, 0 to destroy.");

        await ExecuteTerraformAsync("apply -auto-approve",
            expectedOutput: "Apply complete! Resources: 3 added, 0 changed, 0 destroyed.");

        await ExecuteTerraformAsync("state list",
            expectedOutput: "local_file.test\nnull_resource.test_backend\nrandom_string.test");

        await ExecuteTerraformAsync("apply -auto-approve",
            expectedOutput: "Apply complete! Resources: 0 added, 0 changed, 0 destroyed.");

        await ExecuteTerraformAsync("destroy -auto-approve",
            expectedOutput: "Destroy complete! Resources: 3 destroyed.");
    }
}
