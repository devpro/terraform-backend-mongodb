using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Resources;

[Trait("Category", "IntegrationTests")]
public class HealthCheckResourceTest(TestWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    [Trait("Mode", "Readonly")]
    public async Task HealthCheckResource_Get_ReturnsOk()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        // Assert
        await CheckResponseAndGetContentAsync(response, HttpStatusCode.OK, "text/plain", "Healthy",
            cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Mode", "Readonly")]
    public async Task HealthCheckResource_GetWithUnreachableDatabase_ReturnsServiceUnavailable()
    {
        // Arrange
        var client = CreateClient(builderConfiguration: builder => builder.UseSetting(
            "DatabaseSettings:ConnectionString",
            "mongodb://localhost:27016/?directConnection=true&serverSelectionTimeoutMS=500&connectTimeoutMS=500"));

        // Act
        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        // Assert
        await CheckResponseAndGetContentAsync(response, HttpStatusCode.ServiceUnavailable, "text/plain", "Unhealthy",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
