using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Resources
{
    [Trait("Category", "IntegrationTests")]
    public class HealthCheckResourceTest(WebApplicationFactory<Program> factory)
        : IntegrationTestBase(factory)
    {
        [Fact]
        [Trait("Mode", "Readonly")]
        public async Task HealthCheckResource_Get_ReturnsOk()
        {
            // Arrange
            var client = CreateClient();

            // Act
            var response = await client.GetAsync("/health");

            // Assert
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
            response.Content.Headers.ContentType.Should().NotBeNull();
            response.Content.Headers.ContentType?.ToString().Should().Be("text/plain");
            var result = await response.Content.ReadAsStringAsync();
            result.Should().NotBeNull();
            result.Should().Be("Healthy");
        }
    }
}
