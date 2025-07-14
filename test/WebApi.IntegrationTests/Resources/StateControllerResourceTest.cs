using System.Net;
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
        await response.CheckResponseAndGetContent(HttpStatusCode.NoContent, null);
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
        await response.CheckResponseAndGetContent(HttpStatusCode.OK, null, string.Empty);
    }

    [Fact]
    public async Task StateResource_LockLifeCycle_IsSuccess()
    {
        // Arrange
        var client = CreateClient(true);
        var name = Faker.Random.Word();
        var stateLock = StateLockFaker.Generate();
        var payload = GeneratePayload();

        // Act & Assert
        var createLockResponse = await client.PostAsync($"/state/{name}/lock", Serialize(stateLock));
        await createLockResponse.CheckResponseAndGetContent(HttpStatusCode.OK, "application/json; charset=utf-8", null);
        var deleteLockRequest = new HttpRequestMessage(HttpMethod.Delete, $"/state/{name}/lock")
        {
            Content = Serialize(stateLock)
        };
        var missingLockIdUpdateResponse = await client.PostAsync($"/state/{name}", payload);
        await missingLockIdUpdateResponse.CheckResponseAndGetContent(HttpStatusCode.Locked, "application/json; charset=utf-8", "{\"message\":\"The state is locked.\"}");
        var wrongLockIdUpdateResponse = await client.PostAsync($"/state/{name}?ID=1234", payload);
        await wrongLockIdUpdateResponse.CheckResponseAndGetContent(HttpStatusCode.Conflict, "text/plain; charset=utf-8", "LockId doesn't match with the existing lock");
        var updateResponse = await client.PostAsync($"/state/{name}?ID={stateLock.Id}", payload);
        await updateResponse.CheckResponseAndGetContent(HttpStatusCode.OK, null, string.Empty);
        var deleteLockResponse = await client.SendAsync(deleteLockRequest);
        await deleteLockResponse.CheckResponseAndGetContent(HttpStatusCode.OK, null);
    }
}
