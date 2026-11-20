using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using Devpro.TerraformBackend.Infrastructure.MongoDb.Repositories;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Resources;

/// <summary>
/// Tenant isolation: a caller reaches the states of its own tenant only.
/// <para>
/// Every check here needs a second real account on a second tenant.
/// Asserting isolation with one account only proves that a request naming a tenant the caller has no claim for is refused, which is the easy half.
/// What matters is that a caller who is perfectly well authenticated still cannot reach another tenant's state, and that a refused request changes nothing.
/// </para>
/// </summary>
[Trait("Category", "IntegrationTests")]
public class TenantIsolationTest(TestWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task State_WithTheSameNameInTwoTenants_IsIsolated()
    {
        // Arrange: the unique index is on {tenant, name}, so the same name in two tenants is legitimate and is exactly the case where a missing tenant filter would surface
        var name = UniqueStateName();
        TrackState(TestCredentials.Tenant, name);
        TrackState(TestCredentials.OtherTenant, name);

        var client = CreateClient(true);
        var otherClient = CreateClientFor(TestCredentials.OtherUsername, TestCredentials.OtherPassword);

        await client.PostAsync($"/{TestCredentials.Tenant}/state/{name}",
            Serialize(new { owner = "first", serial = 1 }), TestContext.Current.CancellationToken);
        await otherClient.PostAsync($"/{TestCredentials.OtherTenant}/state/{name}",
            Serialize(new { owner = "second", serial = 2 }), TestContext.Current.CancellationToken);

        // Act
        var first = await client.GetStringAsync($"/{TestCredentials.Tenant}/state/{name}",
            TestContext.Current.CancellationToken);
        var second = await otherClient.GetStringAsync($"/{TestCredentials.OtherTenant}/state/{name}",
            TestContext.Current.CancellationToken);

        // Assert
        first.Should().Contain("first").And.NotContain("second");
        second.Should().Contain("second").And.NotContain("first");
    }

    [Fact]
    public async Task State_ReadFromAnotherTenant_IsRefused()
    {
        // Arrange
        var name = UniqueStateName();
        TrackState(TestCredentials.Tenant, name);
        var client = CreateClient(true);
        await client.PostAsync($"/{TestCredentials.Tenant}/state/{name}",
            Serialize(new { owner = "first" }), TestContext.Current.CancellationToken);

        // Act: a fully authenticated caller from the other tenant asks for it by its real path
        var otherClient = CreateClientFor(TestCredentials.OtherUsername, TestCredentials.OtherPassword);
        var response = await otherClient.GetAsync($"/{TestCredentials.Tenant}/state/{name}",
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task State_DeletedFromAnotherTenant_IsRefusedAndLeftIntact()
    {
        // Arrange
        var name = UniqueStateName();
        TrackState(TestCredentials.Tenant, name);
        var client = CreateClient(true);
        await client.PostAsync($"/{TestCredentials.Tenant}/state/{name}",
            Serialize(new { owner = "first" }), TestContext.Current.CancellationToken);

        // Act
        var otherClient = CreateClientFor(TestCredentials.OtherUsername, TestCredentials.OtherPassword);
        var response = await otherClient.DeleteAsync($"/{TestCredentials.Tenant}/state/{name}",
            TestContext.Current.CancellationToken);

        // Assert: the status code alone would not prove the state survived, which is the thing that matters
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var surviving = await client.GetStringAsync($"/{TestCredentials.Tenant}/state/{name}",
            TestContext.Current.CancellationToken);
        surviving.Should().Contain("first");
    }

    [Fact]
    public async Task State_WrittenToAnotherTenant_IsRefusedAndLeavesTheStateUnchanged()
    {
        // Arrange
        var name = UniqueStateName();
        TrackState(TestCredentials.Tenant, name);
        var client = CreateClient(true);
        await client.PostAsync($"/{TestCredentials.Tenant}/state/{name}",
            Serialize(new { owner = "first" }), TestContext.Current.CancellationToken);

        // Act
        var otherClient = CreateClientFor(TestCredentials.OtherUsername, TestCredentials.OtherPassword);
        var response = await otherClient.PostAsync($"/{TestCredentials.Tenant}/state/{name}",
            Serialize(new { owner = "overwritten" }), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var unchanged = await client.GetStringAsync($"/{TestCredentials.Tenant}/state/{name}",
            TestContext.Current.CancellationToken);
        unchanged.Should().Contain("first").And.NotContain("overwritten");
    }

    [Fact]
    [Trait("Mode", "Readonly")]
    public async Task State_WrittenToAnotherTenantWithAMalformedBody_IsRefusedBeforeTheBodyIsRead()
    {
        // Arrange: a body that fails to parse answers 400 if it is read, so a 401 proves the tenant was checked first
        var otherClient = CreateClientFor(TestCredentials.OtherUsername, TestCredentials.OtherPassword);

        // Act
        var response = await otherClient.PostAsync($"/{TestCredentials.Tenant}/state/{UniqueStateName()}",
            new StringContent("{not json", Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task StateLock_TakenOnAnotherTenant_IsRefusedAndLeavesTheStateUnlocked()
    {
        // Arrange
        var name = UniqueStateName();
        TrackState(TestCredentials.Tenant, name);
        var client = CreateClient(true);
        var otherClient = CreateClientFor(TestCredentials.OtherUsername, TestCredentials.OtherPassword);

        // Act
        var response = await otherClient.PostAsync($"/{TestCredentials.Tenant}/state/{name}/lock",
            Serialize(StateLockFaker.Generate()), TestContext.Current.CancellationToken);

        // Assert: a lock that slipped through would block the owning tenant's next apply, so the check is that the owner can still take the lock afterwards
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var ownLock = StateLockFaker.Generate();
        var ownResponse = await client.PostAsync($"/{TestCredentials.Tenant}/state/{name}/lock",
            Serialize(ownLock), TestContext.Current.CancellationToken);
        ownResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task State_AfterATenantRenameWithACachedCredential_IsServedOnTheNewAddressOnly()
    {
        // Arrange: a user verified once, so its credential is cached, then renamed to another tenant together with its state,
        // as a tenant rename in another application sharing the database does
        var username = $"rename-test-{Guid.NewGuid():N}";
        const string password = "rename123";
        var oldTenant = $"old-{Guid.NewGuid():N}";
        var newTenant = $"new-{Guid.NewGuid():N}";
        var name = UniqueStateName();
        var otherName = UniqueStateName();
        TrackDocumentsWhere("user", Builders<BsonDocument>.Filter.Eq("username", username));
        TrackState(oldTenant, name);
        TrackState(newTenant, name);
        TrackState(oldTenant, otherName);

        var database = Factory.Services.GetRequiredService<IMongoDatabase>();
        await database.GetCollection<BsonDocument>("user").InsertOneAsync(new BsonDocument
        {
            ["username"] = username,
            ["password_hash"] = BCrypt.Net.BCrypt.HashPassword(password, UserRepository.StoredHashWorkFactor),
            ["tenant"] = oldTenant
        }, cancellationToken: TestContext.Current.CancellationToken);

        var client = CreateClientFor(username, password);
        (await client.PostAsync($"/{oldTenant}/state/{name}", Serialize(new { owner = "moved", serial = 1 }),
            TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.OK);

        await database.GetCollection<BsonDocument>("user").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("username", username),
            Builders<BsonDocument>.Update.Set("tenant", newTenant), cancellationToken: TestContext.Current.CancellationToken);
        await database.GetCollection<BsonDocument>("tf_state").UpdateManyAsync(
            Builders<BsonDocument>.Filter.Eq("tenant", oldTenant),
            Builders<BsonDocument>.Update.Set("tenant", newTenant), cancellationToken: TestContext.Current.CancellationToken);

        // Act: at once, with no wait
        var oldRead = await client.GetAsync($"/{oldTenant}/state/{name}", TestContext.Current.CancellationToken);
        var oldWrite = await client.PostAsync($"/{oldTenant}/state/{name}", Serialize(new { owner = "orphan", serial = 2 }),
            TestContext.Current.CancellationToken);
        var oldNewName = await client.PostAsync($"/{oldTenant}/state/{otherName}", Serialize(new { owner = "orphan", serial = 1 }),
            TestContext.Current.CancellationToken);
        var oldLock = await client.PostAsync($"/{oldTenant}/state/{name}/lock", Serialize(StateLockFaker.Generate()),
            TestContext.Current.CancellationToken);
        var newRead = await client.GetStringAsync($"/{newTenant}/state/{name}", TestContext.Current.CancellationToken);

        // Assert: a 404 on the old address reads as "no state" to Terraform, and a 200 on a write leaves an orphan state under the old tenant
        oldRead.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        oldWrite.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        oldNewName.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        oldLock.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        newRead.Should().Contain("moved");
        (await database.GetCollection<BsonDocument>("tf_state").CountDocumentsAsync(
            Builders<BsonDocument>.Filter.Eq("tenant", oldTenant), cancellationToken: TestContext.Current.CancellationToken)).Should().Be(0);
    }

    private HttpClient CreateClientFor(string username, string password)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}")));
        return client;
    }
}
