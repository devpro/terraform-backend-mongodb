using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests;

/// <summary>
/// Base for every test that writes to the suite's database, holding the one thing all of them need: a registry of "undo this" actions that runs when the test ends, whether it passed or failed.
/// <para>
/// The suite runs against a real, long-lived MongoDB rather than a throwaway one per test, so a test that leaves documents behind is not merely untidy.
/// <c>tf_state</c> and <c>tf_state_lock</c> carry unique indexes on <c>{tenant, name}</c>, so yesterday's leftover makes today's run fail with a duplicate key.
/// Leaving the database as it was found is what keeps the suite re-runnable.
/// </para>
/// <para>
/// Registration is used rather than a per-test <c>try</c>/<c>finally</c>.
/// A <c>finally</c> only covers what was created before the <c>try</c> opened, so the common "create two fixtures, then open the try" shape leaks whenever the second create fails.
/// Registering at the moment of creation has no such gap.
/// </para>
/// </summary>
public abstract class DatabaseTestBase(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly List<Func<Task>> _cleanups = [];

    protected TestWebApplicationFactory Factory { get; } = factory;

    /// <summary>
    /// Registers an action that undoes something this test created.
    /// Call it as soon as the thing exists, never at the end of the test.
    /// </summary>
    protected void TrackCleanup(Func<Task> cleanup) => _cleanups.Add(cleanup);

    /// <summary>
    /// Registers every document a state is made of for removal: the <c>tf_state</c> document itself, its lock if one is still held, and its <c>tf_state_history</c> entries.
    /// <para>
    /// The history is the part that is easy to miss, and it is not a test-only concern: <c>StateRepository.DeleteAsync</c> removes only the <c>tf_state</c> document, so deleting a state through the API leaves its history behind forever, here and in production alike (see B-50).
    /// A test that only calls <c>DELETE</c> therefore still leaks.
    /// </para>
    /// </summary>
    protected void TrackState(string tenant, string name)
    {
        TrackDocumentsWhere("tf_state", Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("tenant", tenant),
            Builders<BsonDocument>.Filter.Eq("name", name)));
        TrackDocumentsWhere("tf_state_lock", Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("tenant", tenant),
            Builders<BsonDocument>.Filter.Eq("name", name)));
        TrackDocumentsWhere("tf_state_history", Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("tenant", tenant),
            Builders<BsonDocument>.Filter.Eq("name", name)));
    }

    /// <summary>
    /// Registers every document matching a filter for deletion.
    /// <para>
    /// Filters are built over <see cref="BsonDocument"/> with explicit field names, which is safe here because every collection this suite touches is keyed by the <c>tenant</c> and <c>name</c> string fields rather than by <c>_id</c>.
    /// Filtering a mapped collection by the string field name <c>"_id"</c> is the trap to avoid: it compares a BSON string against an <see cref="ObjectId"/>, matches nothing, deletes nothing, and reports success.
    /// </para>
    /// </summary>
    protected void TrackDocumentsWhere(string collectionName, FilterDefinition<BsonDocument> filter)
    {
        TrackCleanup(async () =>
        {
            var collection = Factory.Services.GetRequiredService<IMongoDatabase>()
                .GetCollection<BsonDocument>(collectionName);
            await collection.DeleteManyAsync(filter, CancellationToken.None);
        });
    }

    public virtual ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Runs every registered cleanup in reverse order of registration, so a child is removed before the parent it references.
    /// <para>
    /// The list is drained rather than indexed over a cached count, because a cleanup may register more.
    /// Cleanups run under <see cref="CancellationToken.None"/>, never <c>TestContext.Current.CancellationToken</c>: that token is cancelled exactly when a test times out or the run is interrupted, which is precisely when leftovers are most likely and cleanup matters most.
    /// One failing cleanup never skips the rest; failures are collected and reported together.
    /// </para>
    /// </summary>
    public virtual async ValueTask DisposeAsync()
    {
        List<Exception>? failures = null;

        while (_cleanups.Count > 0)
        {
            var cleanup = _cleanups[^1];
            _cleanups.RemoveAt(_cleanups.Count - 1);

            try
            {
                await cleanup();
            }
            catch (Exception exception)
            {
                (failures ??= []).Add(exception);
            }
        }

        GC.SuppressFinalize(this);

        if (failures is not null)
        {
            throw new AggregateException(
                $"{failures.Count} test cleanup action(s) failed, so the test database may still hold data " +
                "created by this test.", failures);
        }
    }
}
