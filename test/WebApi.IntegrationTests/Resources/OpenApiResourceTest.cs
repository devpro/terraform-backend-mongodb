using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using VerifyXunit;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Resources;

[Trait("Category", "IntegrationTests")]
public class OpenApiResourceTest(WebApplicationFactory<Program> factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    [Trait("Mode", "Readonly")]
    public async Task OpenApiResource_Get_ReturnsOk()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        // Assert
        var content = await CheckResponseAndGetContentAsync(response, HttpStatusCode.OK, "application/json; charset=utf-8",
            cancellationToken: TestContext.Current.CancellationToken);
        await Verifier.VerifyJson(content)
            .UseDirectory("../Snapshots")
            .UseFileName("OpenApiResource.json");
    }
}
