using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Xunit.Abstractions;

namespace Kalosyni.TerraformBackend.WebApi.IntegrationTests.Resources
{
    [Trait("Category", "IntegrationTests")]
    public class HealthCheckResourceTest : ResourceBase
    {
        private const string ResourceEndpoint = "health";

        public HealthCheckResourceTest(WebApplicationFactory<Program> factory, ITestOutputHelper testOutput)
            : base(factory, testOutput)
        {
        }

        [Fact]
        [Trait("Mode", "Readonly")]
        public async Task HealthCheckResource_Get_ReturnsOk()
        {
            var output = await GetAsync($"/{ResourceEndpoint}");
            output.Should().NotBeNull();
            output.Should().Be("Healthy");
        }
    }
}
