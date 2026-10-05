using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Scripts;

/// <summary>
/// <c>tfbeadm migrate-created-at</c>, which renames the pre-existing camelCase <c>createdAt</c> to <c>created_at</c> in <c>tf_state</c> and <c>tf_state_history</c>.
/// <para>
/// Every document these tests seed is written the way a deployment from before 1.3.0 holds it, with <c>createdAt</c> and no <c>created_at</c>, rather than through the application, which writes <c>created_at</c> and would never exercise the rename.
/// </para>
/// </summary>
[Trait("Category", "IntegrationTests")]
public class MigrateCreatedAtTest(TestWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task MigrateCreatedAt_RenamesTheFieldInBothCollections()
    {
        // Arrange
        var name = UniqueStateName();
        TrackState(TestCredentials.Tenant, name);
        var database = Factory.Services.GetRequiredService<IMongoDatabase>();

        await database.GetCollection<BsonDocument>("tf_state").InsertOneAsync(new BsonDocument
        {
            ["tenant"] = TestCredentials.Tenant,
            ["name"] = name,
            ["createdAt"] = new BsonDateTime(DateTime.UtcNow),
            ["value"] = new BsonDocument("version", 4)
        }, cancellationToken: TestContext.Current.CancellationToken);
        await database.GetCollection<BsonDocument>("tf_state_history").InsertOneAsync(new BsonDocument
        {
            ["tenant"] = TestCredentials.Tenant,
            ["name"] = name,
            ["createdAt"] = new BsonDateTime(DateTime.UtcNow),
            ["upgrade"] = "[]"
        }, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var result = await TfbeadmRunner.RunAsync(["migrate-created-at"], password: null,
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.Should().Be(0, "tfbeadm failed: {0}{1}", result.StandardOutput, result.StandardError);

        var state = await FindByName(database, "tf_state", name);
        state.Should().NotBeNull();
        state!.Contains("createdAt").Should().BeFalse("the old field name must not survive the migration");
        state.Contains("created_at").Should().BeTrue();

        var history = await FindByName(database, "tf_state_history", name);
        history.Should().NotBeNull();
        history!.Contains("createdAt").Should().BeFalse();
        history.Contains("created_at").Should().BeTrue();
    }

    /// <summary>
    /// Proves the command is safe to re-run: neither an operator running it twice out of caution, nor running it against a database where some documents were already migrated and others were not, must disturb a document that is already correct.
    /// </summary>
    [Fact]
    public async Task MigrateCreatedAt_RunTwice_LeavesAnAlreadyMigratedDocumentUnchanged()
    {
        // Arrange
        var name = UniqueStateName();
        TrackState(TestCredentials.Tenant, name);
        var database = Factory.Services.GetRequiredService<IMongoDatabase>();
        var originalTimestamp = new BsonDateTime(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        await database.GetCollection<BsonDocument>("tf_state").InsertOneAsync(new BsonDocument
        {
            ["tenant"] = TestCredentials.Tenant,
            ["name"] = name,
            ["createdAt"] = originalTimestamp,
            ["value"] = new BsonDocument("version", 4)
        }, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var first = await TfbeadmRunner.RunAsync(["migrate-created-at"], password: null,
            TestContext.Current.CancellationToken);
        first.ExitCode.Should().Be(0, "tfbeadm failed: {0}{1}", first.StandardOutput, first.StandardError);
        var second = await TfbeadmRunner.RunAsync(["migrate-created-at"], password: null,
            TestContext.Current.CancellationToken);

        // Assert
        second.ExitCode.Should().Be(0, "tfbeadm failed: {0}{1}", second.StandardOutput, second.StandardError);
        var state = await FindByName(database, "tf_state", name);
        state.Should().NotBeNull();
        state!["created_at"].Should().Be(originalTimestamp, "a document already migrated must keep its original value");
    }

    private static async Task<BsonDocument?> FindByName(IMongoDatabase database, string collectionName, string name) =>
        await database.GetCollection<BsonDocument>(collectionName)
            .Find(Builders<BsonDocument>.Filter.Eq("name", name))
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
}
