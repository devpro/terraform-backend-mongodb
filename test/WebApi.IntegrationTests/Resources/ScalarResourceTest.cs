using System.Net;
using System.Threading.Tasks;
using AwesomeAssertions;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Resources;

[Trait("Category", "IntegrationTests")]
public class ScalarResourceTest(TestWebApplicationFactory factory)
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

    [Fact]
    [Trait("Mode", "Readonly")]
    public async Task ScalarResource_GetWithFeatureDisabled_ReturnsNotFound()
    {
        // Arrange
        var client = CreateClient(builderConfiguration: builder =>
            builder.UseSetting("Features:IsScalarEnabled", "false"));

        // Act
        var response = await client.GetAsync("/scalar", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
