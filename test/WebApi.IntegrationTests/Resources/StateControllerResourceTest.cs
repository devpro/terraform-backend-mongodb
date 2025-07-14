using Devpro.TerraformBackend.WebApi.IntegrationTests.Http;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Resources;

[Trait("Category", "IntegrationTests")]
public class StateControllerResourceTest(WebApplicationFactory<Program> factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task HealthCheckResource_GetNotExisting_ReturnsOk()
    {
        // Arrange
        var client = CreateClient(true);
        var name = Faker.Random.Word();
        var lockId = Faker.Random.Guid().ToString();

        // Act
        var response = await client.GetAsync($"/state/{name}?ID={lockId}");

        // Assert
        await response.CheckResponseAndGetContent(System.Net.HttpStatusCode.OK, "text/plain; charset=utf-8", string.Empty);
    }

    [Fact]
    public async Task HealthCheckResource_CreateNew_ReturnsCreated()
    {
        // Arrange
        var client = CreateClient(true);
        var name = Faker.Random.Word();
        var lockId = Faker.Random.Guid().ToString();
        var payload = GeneratePayload();

        // Act
        var response = await client.PostAsync($"/state/{name}?ID={lockId}", payload);

        // Assert
        //TODO: test resource URL in response
        await response.CheckResponseAndGetContent(System.Net.HttpStatusCode.Created, null, string.Empty);
    }
}
