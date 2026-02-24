using System.Threading.Tasks;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Resources;

[Trait("Category", "IntegrationTests")]
public class SwaggerResourceTest(WebApplicationFactory<Program> factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    [Trait("Mode", "Readonly")]
    public async Task SwaggerResource_Get_ReturnsOk()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/index.html");

        // Assert
        await response.CheckResponseAndGetContent(System.Net.HttpStatusCode.OK, "text/html; charset=utf-8");
    }
}
