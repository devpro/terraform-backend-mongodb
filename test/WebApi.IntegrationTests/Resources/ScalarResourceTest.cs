using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Resources;

[Trait("Category", "IntegrationTests")]
public class ScalarResourceTest(WebApplicationFactory<Program> factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    [Trait("Mode", "Readonly")]
    public async Task ScalarResource_Get_ReturnsOk()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.GetAsync("/scalar", TestContext.Current.CancellationToken);

        // Assert
        await CheckResponseAndGetContentAsync(response, HttpStatusCode.OK, "text/html",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
