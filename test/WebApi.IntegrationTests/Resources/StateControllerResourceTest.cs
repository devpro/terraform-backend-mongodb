using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AwesomeAssertions;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Resources;

[Trait("Category", "IntegrationTests")]
public class StateControllerResourceTest(TestWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const string Tenant = TestCredentials.Tenant;

    [Fact]
    [Trait("Mode", "Readonly")]
    public async Task StateResource_GetNotExisting_ReturnsNoContent()
    {
        // Arrange
        var client = CreateClient(true);
        var name = UniqueStateName();

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
        var name = UniqueStateName();

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
        var request = new HttpRequestMessage(HttpMethod.Get, $"/{Tenant}/state/{UniqueStateName()}");
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
        var name = UniqueStateName();
        var state = NewState();
        TrackState(Tenant, name);

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
        var name = UniqueStateName();
        var state = NewState();
        var stateLock = StateLockFaker.Generate();
        // this test posts the state twice, so it also writes a tf_state_history entry, and it never deletes
        // the state itself
        TrackState(Tenant, name);

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

    [Fact]
    [Trait("Mode", "Readonly")]
    public async Task StateResource_CreateWithoutContentType_IsUnsupportedMediaType()
    {
        // Arrange: M1, a body with no Content-Type header at all, which Terraform never sends
        var client = CreateClient(true);
        var content = new ByteArrayContent("{\"version\":4}"u8.ToArray());

        // Act
        var response = await client.PostAsync($"/{Tenant}/state/{UniqueStateName()}", content, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task StateResource_WithANameOfDigitsOnly_IsRouted()
    {
        // Arrange: L1, the name is not constrained, so a name with no letter reaches the controller
        var client = CreateClient(true);
        var name = Faker.Random.Number(100_000, 999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        TrackState(Tenant, name);

        // Act
        var createResponse = await client.PostAsync($"/{Tenant}/state/{name}", Serialize(NewState()), TestContext.Current.CancellationToken);

        // Assert
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task StateLockResource_SameLockIdOnTwoStates_LocksAndUnlocksEach()
    {
        // Arrange: L12, a lock ID is scoped to its state, so reusing one elsewhere must not collide
        var client = CreateClient(true);
        var names = new[] { UniqueStateName(), UniqueStateName() };
        var stateLock = StateLockFaker.Generate();
        foreach (var name in names)
        {
            TrackState(Tenant, name);
        }

        foreach (var name in names)
        {
            // Act
            var lockResponse = await client.PostAsync($"/{Tenant}/state/{name}/lock", Serialize(stateLock), TestContext.Current.CancellationToken);

            // Assert
            var lockContent = await lockResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            lockResponse.StatusCode.Should().Be(HttpStatusCode.OK, lockContent);
            lockContent.Should().Contain($"\"name\":\"{name}\"");
        }

        foreach (var name in names)
        {
            var unlockRequest = new HttpRequestMessage(HttpMethod.Delete, $"/{Tenant}/state/{name}/lock")
            {
                Content = Serialize(stateLock)
            };
            var unlockResponse = await client.SendAsync(unlockRequest, TestContext.Current.CancellationToken);
            unlockResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
