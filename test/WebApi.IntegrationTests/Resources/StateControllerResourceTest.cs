using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Bogus;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Resources;

[Trait("Category", "IntegrationTests")]
public class StateControllerResourceTest(WebApplicationFactory<Program> factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task HealthCheckResource_Create_ReturnsOk()
    {
        // Arrange
        var client = CreateClient();
        var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", "admin", "admin")));
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authToken);
        var name = Faker.Random.Word();
        var lockId = Faker.Random.Guid().ToString();
        var payload = new
        {
            Property1 = "Value1",
            Property2 = 123
        };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync($"/state/{name}?ID={lockId}", content);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        response.Content.Headers.ContentType.Should().BeNull();
        //TODO: test resource URL
        var result = await response.Content.ReadAsStringAsync();
        result.Should().NotBeNull();
        result.ToString().Should().BeEmpty();
    }
}
