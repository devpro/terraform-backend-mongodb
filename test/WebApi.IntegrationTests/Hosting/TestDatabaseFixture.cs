using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Devpro.TerraformBackend.Infrastructure.MongoDb.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;

/// <summary>
/// Owns the suite's database for the length of a run: it creates the indexes, seeds the account every test
/// authenticates as, and proves on the way out that the run left the database as it found it.
/// <para>
/// Seeding here is what ends the drift. The account used to be created by hand with
/// <c>tfbeadm create-user admin admin123 dummy</c> in the database the maintainer also works in, so it
/// eventually belonged to another tenant and every authenticating test failed with a bare <c>401</c> that
/// says nothing about the cause. A run that creates its own account in its own database cannot drift.
/// </para>
/// <para>
/// The closing count check is the other half. Cleanup that silently does nothing looks exactly like cleanup
/// that worked, so a run compares every collection against the baseline it took before seeding and fails
/// naming the collection that grew. It reports rather than deletes: removing documents this run did not
/// create would hide the leak instead of surfacing it.
/// </para>
/// </summary>
public sealed class TestDatabaseFixture : IAsyncLifetime
{
    private static readonly string[] TrackedCollections = ["tf_state", "tf_state_lock", "tf_state_history", "user"];

    private readonly Dictionary<string, long> _baselineCounts = [];

    private IMongoDatabase _database = null!;

    private ObjectId _seededUserId;

    public async ValueTask InitializeAsync()
    {
        var databaseName = IntegrationTestDatabase.Name;
        TestDatabaseGuard.EnsureTestDatabaseName(databaseName);

        _database = new MongoClient(IntegrationTestDatabase.ConnectionString).GetDatabase(databaseName);

        await CreateIndexesAsync();
        await CaptureBaselineAsync();
        await SeedUserAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await RemoveSeededUserAsync();
        await VerifyDatabaseWasLeftAsFoundAsync();
    }

    /// <summary>
    /// Mirrors the indexes <c>scripts/tfbeadm create-indexes</c> creates, so the suite exercises the same
    /// uniqueness constraints production runs under. Index creation is idempotent, so a re-run is free.
    /// </summary>
    private async Task CreateIndexesAsync()
    {
        var tenantAndName = Builders<BsonDocument>.IndexKeys.Ascending("tenant").Ascending("name");

        await _database.GetCollection<BsonDocument>("tf_state").Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(tenantAndName, new CreateIndexOptions { Unique = true }),
            cancellationToken: CancellationToken.None);
        await _database.GetCollection<BsonDocument>("tf_state_lock").Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(tenantAndName, new CreateIndexOptions { Unique = true }),
            cancellationToken: CancellationToken.None);
        await _database.GetCollection<BsonDocument>("tf_state_history").Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(tenantAndName),
            cancellationToken: CancellationToken.None);
        await _database.GetCollection<BsonDocument>("user").Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("username"),
                new CreateIndexOptions { Unique = true }),
            cancellationToken: CancellationToken.None);
    }

    private async Task CaptureBaselineAsync()
    {
        foreach (var collectionName in TrackedCollections)
        {
            _baselineCounts[collectionName] = await CountAsync(collectionName);
        }
    }

    /// <summary>
    /// Writes the account as a raw <see cref="BsonDocument"/> with literal field names rather than through
    /// <c>UserModel</c>, because the camelCase convention pack that maps that model is registered while the
    /// host is being built and this fixture runs before any host exists.
    /// </summary>
    private async Task SeedUserAsync()
    {
        var collection = _database.GetCollection<BsonDocument>("user");

        // a leftover from an interrupted run would fail the unique index on username
        await collection.DeleteManyAsync(
            Builders<BsonDocument>.Filter.Eq("username", TestCredentials.Username),
            CancellationToken.None);

        _seededUserId = ObjectId.GenerateNewId();
        await collection.InsertOneAsync(new BsonDocument
        {
            ["_id"] = _seededUserId,
            ["username"] = TestCredentials.Username,
            // the work factor is taken from the application rather than left to the library default, which is
            // 11: seeding at a different cost from the one production writes (10, through htpasswd in
            // tfbeadm) makes the seeded account slower to verify than the dummy hash, which shows up as a
            // timing difference that AuthenticationTimingTest correctly reports as an enumeration oracle
            ["password_hash"] = BCrypt.Net.BCrypt.HashPassword(
                TestCredentials.Password, UserRepository.StoredHashWorkFactor),
            ["tenant"] = TestCredentials.Tenant
        }, cancellationToken: CancellationToken.None);
    }

    private async Task RemoveSeededUserAsync()
    {
        if (_seededUserId == ObjectId.Empty) return;

        await _database.GetCollection<BsonDocument>("user").DeleteOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", _seededUserId),
            CancellationToken.None);
    }

    /// <summary>
    /// Compares every tracked collection against the baseline and throws naming the ones that grew.
    /// <para>
    /// This is the assertion that the per-test cleanup registry in <c>DatabaseTestBase</c> actually works. A
    /// delete filter that matches nothing deletes nothing and reports success, so a passing suite is not
    /// evidence on its own.
    /// </para>
    /// </summary>
    private async Task VerifyDatabaseWasLeftAsFoundAsync()
    {
        var leaks = new List<string>();

        foreach (var collectionName in TrackedCollections)
        {
            var finalCount = await CountAsync(collectionName);
            var baseline = _baselineCounts[collectionName];
            if (finalCount != baseline)
            {
                leaks.Add($"{collectionName}: {baseline} before, {finalCount} after ({finalCount - baseline:+#;-#;0})");
            }
        }

        if (leaks.Count > 0)
        {
            throw new InvalidOperationException(
                $"The test run did not leave '{IntegrationTestDatabase.Name}' as it found it: " +
                $"{string.Join("; ", leaks)}. A test created documents without registering their cleanup, or a " +
                "cleanup filter matched nothing. The documents are deliberately left in place for inspection.");
        }
    }

    private async Task<long> CountAsync(string collectionName) =>
        await _database.GetCollection<BsonDocument>(collectionName)
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: CancellationToken.None);
}
