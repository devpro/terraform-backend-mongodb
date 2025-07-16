using System.Net;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Http;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Resources;

[Trait("Category", "IntegrationTests")]
public class StateControllerResourceTest(WebApplicationFactory<Program> factory)
    : IntegrationTestBase(factory)
{
    private const string Tenant = "dummy";

    [Fact]
    [Trait("Mode", "Readonly")]
    public async Task StateResource_GetNotExisting_ReturnsNoContent()
    {
        // Arrange
        var client = CreateClient(true);
        var name = Faker.Random.Word();

        // Act
        var response = await client.GetAsync($"/{Tenant}/state/{name}");

        // Assert
        await response.CheckResponseAndGetContent(HttpStatusCode.NoContent, null);
    }

    [Fact]
    public async Task StateResource_CreateFindDelete_IsSuccess()
    {
        // Arrange
        var client = CreateClient(true);
        var name = Faker.Random.Word();
        var state = StateFaker.Generate();

        // Act & Assert
        var createResponse = await client.PostAsync($"/{Tenant}/state/{name}", Serialize(state));
        //TODO: test resource URL in response
        await createResponse.CheckResponseAndGetContent(HttpStatusCode.OK, null, string.Empty);
        var findResponse = await client.GetAsync($"/{Tenant}/state/{name}");
        await findResponse.CheckResponseAndGetContent(HttpStatusCode.OK, "text/plain; charset=utf-8");
        var deleteResponse = await client.DeleteAsync($"/{Tenant}/state/{name}");
        await deleteResponse.CheckResponseAndGetContent(HttpStatusCode.OK, null);
    }

    [Fact]
    public async Task StateResource_LockLifeCycle_IsSuccess()
    {
        // Arrange
        var client = CreateClient(true);
        var name = Faker.Random.Word();
        var state = StateFaker.Generate();
        var stateLock = StateLockFaker.Generate();

        // Act & Assert
        var createLockResponse = await client.PostAsync($"/{Tenant}/state/{name}/lock", Serialize(stateLock));
        await createLockResponse.CheckResponseAndGetContent(HttpStatusCode.OK, "application/json; charset=utf-8", null);
        var missingLockIdUpdateResponse = await client.PostAsync($"/{Tenant}/state/{name}", Serialize(state));
        await missingLockIdUpdateResponse.CheckResponseAndGetContent(HttpStatusCode.Locked, "application/json; charset=utf-8", "{\"message\":\"The state is locked.\"}");
        var wrongLockIdUpdateResponse = await client.PostAsync($"/{Tenant}/state/{name}?ID=1234", Serialize(state));
        await wrongLockIdUpdateResponse.CheckResponseAndGetContent(HttpStatusCode.Conflict, "text/plain; charset=utf-8", "LockId doesn't match with the existing lock");
        var updateResponse = await client.PostAsync($"/{Tenant}/state/{name}?ID={stateLock.Id}", Serialize(state));
        await updateResponse.CheckResponseAndGetContent(HttpStatusCode.OK, null, string.Empty);
        var deleteLockRequest = new HttpRequestMessage(HttpMethod.Delete, $"/{Tenant}/state/{name}/lock")
        {
            Content = Serialize(stateLock)
        };
        var deleteLockResponse = await client.SendAsync(deleteLockRequest);
        await deleteLockResponse.CheckResponseAndGetContent(HttpStatusCode.OK, null);
    }
}
