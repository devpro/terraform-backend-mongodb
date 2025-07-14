using Devpro.TerraformBackend.WebApi.IntegrationTests.Http;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Resources;

[Trait("Category", "IntegrationTests")]
public class StateControllerResourceTest(WebApplicationFactory<Program> factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    [Trait("Mode", "Readonly")]
    public async Task StateResource_GetNotExisting_ReturnsNoContent()
    {
        // Arrange
        var client = CreateClient(true);
        var name = Faker.Random.Word();

        // Act
        var response = await client.GetAsync($"/state/{name}");

        // Assert
        await response.CheckResponseAndGetContent(System.Net.HttpStatusCode.NoContent, null);
    }

    [Fact]
    public async Task StateResource_CreateNew_ReturnsCreated()
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
        await response.CheckResponseAndGetContent(System.Net.HttpStatusCode.OK, null, string.Empty);
    }

    [Fact]
    public async Task StateResource_LockLifeCycle_IsSuccess()
    {
        // Arrange
        var client = CreateClient(true);
        var name = Faker.Random.Word();
        var stateLock = StateLockFaker.Generate();

        // Act & Assert
        var createLockResponse = await client.PostAsync($"/state/{name}/lock", Serialize(stateLock));
        await createLockResponse.CheckResponseAndGetContent(System.Net.HttpStatusCode.OK, "application/json; charset=utf-8", null);
    }
}
