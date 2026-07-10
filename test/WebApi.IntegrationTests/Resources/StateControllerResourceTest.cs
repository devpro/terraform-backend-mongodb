using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

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
        var response = await client.GetAsync($"/{Tenant}/state/{name}", TestContext.Current.CancellationToken);

        // Assert
        await CheckResponseAndGetContentAsync(response, HttpStatusCode.NotFound, "application/problem+json; charset=utf-8",
            cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Mode", "Readonly")]
    public async Task StateResource_GetWrongTenant_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateClient(true);
        var name = Faker.Random.Word();

        // Act
        var response = await client.GetAsync($"/acme/state/{name}", TestContext.Current.CancellationToken);

        // Assert
        await CheckResponseAndGetContentAsync(response, HttpStatusCode.Unauthorized, "application/problem+json; charset=utf-8",
            cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Mode", "Readonly")]
    public async Task StateResource_GetWithMalformedAuthorizationHeader_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/{Tenant}/state/{Faker.Random.Word()}");
        request.Headers.TryAddWithoutValidation("Authorization", "Basic ###not-base64###");

        // Act
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task StateResource_CreateFindDelete_IsSuccess()
    {
        // Arrange
        var client = CreateClient(true);
        var name = Faker.Random.Word();
        var state = StateFaker.Generate();

        // Act & Assert
        var createResponse = await client.PostAsync($"/{Tenant}/state/{name}", Serialize(state), TestContext.Current.CancellationToken);
        await CheckResponseAndGetContentAsync(createResponse, HttpStatusCode.OK, null, string.Empty,
            cancellationToken: TestContext.Current.CancellationToken);

        var findResponse = await client.GetAsync($"/{Tenant}/state/{name}", TestContext.Current.CancellationToken);
        await CheckResponseAndGetContentAsync(findResponse, HttpStatusCode.OK, "text/plain; charset=utf-8",
            cancellationToken: TestContext.Current.CancellationToken);

        var deleteResponse = await client.DeleteAsync($"/{Tenant}/state/{name}", TestContext.Current.CancellationToken);
        await CheckResponseAndGetContentAsync(deleteResponse, HttpStatusCode.OK, null,
            cancellationToken: TestContext.Current.CancellationToken);
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
        var createLockResponse = await client.PostAsync($"/{Tenant}/state/{name}/lock", Serialize(stateLock), TestContext.Current.CancellationToken);
        var lockContent = await CheckResponseAndGetContentAsync(createLockResponse, HttpStatusCode.OK, "application/json; charset=utf-8",
            cancellationToken: TestContext.Current.CancellationToken);

        var missingLockIdUpdateResponse = await client.PostAsync($"/{Tenant}/state/{name}", Serialize(state), TestContext.Current.CancellationToken);
        await CheckResponseAndGetContentAsync(missingLockIdUpdateResponse, HttpStatusCode.Locked, "application/json; charset=utf-8",
            "{\"message\":\"The state is locked.\"}", cancellationToken: TestContext.Current.CancellationToken);

        var wrongLockIdUpdateResponse = await client.PostAsync($"/{Tenant}/state/{name}?ID=1234", Serialize(state), TestContext.Current.CancellationToken);
        await CheckResponseAndGetContentAsync(wrongLockIdUpdateResponse, HttpStatusCode.Conflict, "application/json; charset=utf-8",
            lockContent, cancellationToken: TestContext.Current.CancellationToken);

        var updateResponse = await client.PostAsync($"/{Tenant}/state/{name}?ID={stateLock.Id}", Serialize(state), TestContext.Current.CancellationToken);
        await CheckResponseAndGetContentAsync(updateResponse, HttpStatusCode.OK, null, string.Empty,
            cancellationToken: TestContext.Current.CancellationToken);

        var deleteLockRequest = new HttpRequestMessage(HttpMethod.Delete, $"/{Tenant}/state/{name}/lock")
        {
            Content = Serialize(stateLock)
        };
        var deleteLockResponse = await client.SendAsync(deleteLockRequest, TestContext.Current.CancellationToken);
        await CheckResponseAndGetContentAsync(deleteLockResponse, HttpStatusCode.OK, null,
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
