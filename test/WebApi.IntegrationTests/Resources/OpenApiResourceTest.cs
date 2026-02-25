using System.Threading.Tasks;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Http;
using Microsoft.AspNetCore.Mvc.Testing;
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
        var response = await client.GetAsync("/openapi/v1.json");

        // Assert
        await response.CheckResponseAndGetContent(System.Net.HttpStatusCode.OK, "application/json; charset=utf-8");
    }
}
