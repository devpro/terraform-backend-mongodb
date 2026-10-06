using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AwesomeAssertions;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Scripts;

/// <summary>
/// <c>tfbeadm migrate-lock-id</c>, which copies the lock ID of a <c>tf_state_lock</c> document written with the ID as its <c>_id</c> into <c>lock_id</c>.
/// <para>
/// Every lock these tests seed is written the way a deployment from before 1.3.0 holds it,
/// with the ID as <c>_id</c> and no <c>lock_id</c>, rather than through the application, which never writes that shape.
/// </para>
/// </summary>
[Trait("Category", "IntegrationTests")]
public class MigrateLockIdTest(TestWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task MigrateLockId_LetsALockHeldAcrossTheUpgradeBeReleased()
    {
        // Arrange
        var name = UniqueStateName();
        var lockId = Guid.NewGuid().ToString();
        TrackState(TestCredentials.Tenant, name);
        var locks = Factory.Services.GetRequiredService<IMongoDatabase>().GetCollection<BsonDocument>("tf_state_lock");
        await locks.InsertOneAsync(LegacyLock(lockId, name), cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var result = await TfbeadmRunner.RunAsync(["migrate-lock-id"], password: null, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.Should().Be(0, "tfbeadm failed: {0}{1}", result.StandardOutput, result.StandardError);
        var migrated = await FindByName(locks, name);
        migrated!["lock_id"].AsString.Should().Be(lockId);

        var unlockRequest = new HttpRequestMessage(HttpMethod.Delete, $"/{TestCredentials.Tenant}/state/{name}/lock")
        {
            Content = Serialize(new { ID = lockId })
        };
        var unlockResponse = await CreateClient(true).SendAsync(unlockRequest, TestContext.Current.CancellationToken);
        unlockResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await FindByName(locks, name)).Should().BeNull("unlocking with the migrated ID must release the lock");
    }

    [Fact]
    public async Task MigrateLockId_RunTwice_LeavesAnAlreadyMigratedLockUnchanged()
    {
        // Arrange
        var name = UniqueStateName();
        var lockId = Guid.NewGuid().ToString();
        TrackState(TestCredentials.Tenant, name);
        var locks = Factory.Services.GetRequiredService<IMongoDatabase>().GetCollection<BsonDocument>("tf_state_lock");
        await locks.InsertOneAsync(LegacyLock(lockId, name), cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var first = await TfbeadmRunner.RunAsync(["migrate-lock-id"], password: null, TestContext.Current.CancellationToken);
        first.ExitCode.Should().Be(0, "tfbeadm failed: {0}{1}", first.StandardOutput, first.StandardError);
        var second = await TfbeadmRunner.RunAsync(["migrate-lock-id"], password: null, TestContext.Current.CancellationToken);

        // Assert
        second.ExitCode.Should().Be(0, "tfbeadm failed: {0}{1}", second.StandardOutput, second.StandardError);
        var migrated = await FindByName(locks, name);
        migrated!["lock_id"].AsString.Should().Be(lockId);
        migrated["_id"].AsString.Should().Be(lockId);
    }

    private static BsonDocument LegacyLock(string lockId, string name) => new()
    {
        ["_id"] = lockId,
        ["tenant"] = TestCredentials.Tenant,
        ["name"] = name,
        ["operation"] = "OperationTypeApply",
        ["info"] = "",
        ["who"] = "someone@host",
        ["version"] = "1.9.0",
        ["created"] = "2026-09-24T10:00:00Z",
        ["path"] = ""
    };

    private static async Task<BsonDocument?> FindByName(IMongoCollection<BsonDocument> collection, string name) =>
        await collection.Find(Builders<BsonDocument>.Filter.Eq("name", name))
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
}
