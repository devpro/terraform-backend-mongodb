using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Devpro.TerraformBackend.Infrastructure.MongoDb.History;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Devpro.TerraformBackend.Infrastructure.MongoDb.Repositories;

/// <summary>
/// The writes whose change to <c>tf_state_history</c> is still to be computed, one document per write.
/// <para>
/// An entry holds the version the write replaced, never the new one, so that it stays within the 16 MB document limit: the replaced version already fitted in <c>tf_state</c>.
/// The new version is the value of the next entry of the same state, or the current <c>tf_state</c> document when there is none.
/// </para>
/// <para>
/// Entries of one state are ordered by the Terraform <c>serial</c> of the version they hold, then by <c>saved_at</c>,
/// so the order does not depend on the clocks of the replicas that wrote them.
/// A state without a numeric serial is stored with <c>-1</c> and ordered by <c>saved_at</c> alone.
/// </para>
/// </summary>
public class StateHistoryQueueRepository : RepositoryBase
{
    public const string StatusPending = "pending";

    public const string StatusProcessing = "processing";

    /// <summary>
    /// Set aside: the worker ignores the entry, which stays for inspection until an operator sets it back to pending.
    /// </summary>
    public const string StatusFailed = "failed";

    private static readonly SortDefinition<BsonDocument> VersionOrder = Builders<BsonDocument>.Sort
        .Ascending("serial").Ascending("saved_at").Ascending("_id");

    private static readonly ProjectionDefinition<BsonDocument> WithoutValue = Builders<BsonDocument>.Projection
        .Exclude("value");

    private readonly IMongoCollection<BsonDocument> _bsonCollection;

    public StateHistoryQueueRepository(IMongoDatabase mongoDatabase, ILogger<StateHistoryQueueRepository> logger)
        : base(mongoDatabase, logger)
    {
        _bsonCollection = GetCollection<BsonDocument>();
    }

    protected override string CollectionName => "tf_state_history_queue";

    /// <summary>
    /// Queues the change of one write, within the session of the state write when the server runs transactions.
    /// Returns false when the entry does not fit in a document, which loses that one history entry rather than the state write.
    /// </summary>
    public async Task<bool> EnqueueAsync(IClientSessionHandle? session, ObjectId stateId, string tenant, string name,
        DateTime savedAt, BsonValue replacedValue, CancellationToken cancellationToken)
    {
        var serial = GetSerial(replacedValue);
        var document = new BsonDocument
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["tenant"] = tenant,
            ["name"] = name,
            ["state_id"] = stateId,
            ["serial"] = serial,
            ["saved_at"] = new BsonDateTime(savedAt),
            ["status"] = StatusPending,
            ["attempts"] = 0,
            ["value"] = replacedValue
        };

        try
        {
            if (session == null)
            {
                await _bsonCollection.InsertOneAsync(document, cancellationToken: cancellationToken);
            }
            else
            {
                await _bsonCollection.InsertOneAsync(session, document, cancellationToken: cancellationToken);
            }

            return true;
        }
        catch (FormatException exception)
        {
            // the driver refuses to serialise an oversized document before sending it, so the transaction of the state write is unaffected
            StateHistoryTelemetry.Failed.Add(1, new KeyValuePair<string, object?>("reason", "entry_too_large"));
            Logger.LogError(exception,
                "History entry not recorded for state {Tenant}/{Name}: the version this write replaced, at serial {Serial}, " +
                "is too close to the 16 MB document limit to fit in tf_state_history_queue with the fields of a queue entry. " +
                "The new state is saved and served as usual, only the change from the replaced version is missing from tf_state_history.",
                tenant, name, serial);
            return false;
        }
    }

    /// <summary>
    /// Entries a worker may claim, oldest first, without their value: pending ones, and processing ones whose claim has expired.
    /// </summary>
    public async Task<List<BsonDocument>> FindClaimableAsync(DateTime now, StateHistoryOptions options, string? tenant, string? name,
        int limit, CancellationToken cancellationToken)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Unfinished(now, options),
            Builders<BsonDocument>.Filter.Lt("attempts", options.MaxAttempts),
            StateFilter(tenant, name));
        return await _bsonCollection.Find(filter)
            .Project(WithoutValue)
            .Sort(Builders<BsonDocument>.Sort.Ascending("saved_at"))
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Entries claimed as often as allowed without ever finishing, which is what a diff that kills the process looks like.
    /// </summary>
    public async Task<List<BsonDocument>> FindAbandonedAsync(DateTime now, StateHistoryOptions options, string? tenant, string? name,
        CancellationToken cancellationToken)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Unfinished(now, options),
            Builders<BsonDocument>.Filter.Gte("attempts", options.MaxAttempts),
            StateFilter(tenant, name));
        return await _bsonCollection.Find(filter)
            .Project(WithoutValue)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The first entry of a state not set aside, the only one of that state a worker may claim, so the changes of one state are computed in order.
    /// </summary>
    public async Task<BsonDocument?> FindFirstOfStateAsync(string tenant, string name, CancellationToken cancellationToken)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            GetFilter(tenant, name),
            Builders<BsonDocument>.Filter.Ne("status", StatusFailed));
        return await _bsonCollection.Find(filter)
            .Project(WithoutValue)
            .Sort(VersionOrder)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Claims an entry for this worker, provided no other worker changed it since it was read.
    /// Returns the entry with its value, or null when another worker was first.
    /// </summary>
    public async Task<BsonDocument?> TryClaimAsync(BsonDocument entry, DateTime now, CancellationToken cancellationToken)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("_id", entry["_id"]),
            Builders<BsonDocument>.Filter.Eq("status", entry["status"]),
            Builders<BsonDocument>.Filter.Eq("attempts", entry["attempts"]));
        var update = Builders<BsonDocument>.Update
            .Set("status", StatusProcessing)
            .Set("claimed_at", new BsonDateTime(now))
            .Inc("attempts", 1);
        return await _bsonCollection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
    }

    /// <summary>
    /// The version that followed the one an entry holds, when a later write of the same state is queued too.
    /// </summary>
    public async Task<BsonValue?> FindNextValueAsync(BsonDocument entry, CancellationToken cancellationToken)
    {
        var serial = entry["serial"];
        var savedAt = entry["saved_at"];
        var filter = Builders<BsonDocument>.Filter.And(
            GetFilter(entry["tenant"].AsString, entry["name"].AsString),
            Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Gt("serial", serial),
                Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Eq("serial", serial),
                    Builders<BsonDocument>.Filter.Gt("saved_at", savedAt)),
                Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Eq("serial", serial),
                    Builders<BsonDocument>.Filter.Eq("saved_at", savedAt),
                    Builders<BsonDocument>.Filter.Gt("_id", entry["_id"]))));
        var next = await _bsonCollection.Find(filter)
            .Project(Builders<BsonDocument>.Projection.Include("value"))
            .Sort(VersionOrder)
            .FirstOrDefaultAsync(cancellationToken);
        return next?["value"];
    }

    public async Task DeleteAsync(ObjectId id, CancellationToken cancellationToken) =>
        await _bsonCollection.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id), cancellationToken);

    public async Task MarkFailedAsync(ObjectId id, string error, CancellationToken cancellationToken)
    {
        var update = Builders<BsonDocument>.Update
            .Set("status", StatusFailed)
            .Set("error", error);
        await _bsonCollection.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id), update, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Hands a claimed entry back to the queue, without counting the attempt when the worker is only stopping.
    /// </summary>
    public async Task ReleaseAsync(ObjectId id, bool countAttempt, CancellationToken cancellationToken)
    {
        var update = Builders<BsonDocument>.Update
            .Set("status", StatusPending)
            .Unset("claimed_at");
        if (!countAttempt)
        {
            update = update.Inc("attempts", -1);
        }

        await _bsonCollection.UpdateOneAsync(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("_id", id),
                Builders<BsonDocument>.Filter.Eq("status", StatusProcessing)),
            update, cancellationToken: cancellationToken);
    }

    private static long GetSerial(BsonValue value) =>
        value is BsonDocument document && document.TryGetValue("serial", out var serial) && serial.IsNumeric
            ? serial.ToInt64()
            : -1;

    private static FilterDefinition<BsonDocument> Unfinished(DateTime now, StateHistoryOptions options) =>
        Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Eq("status", StatusPending),
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("status", StatusProcessing),
                Builders<BsonDocument>.Filter.Lt("claimed_at", new BsonDateTime(now - options.ClaimTimeout))));

    private static FilterDefinition<BsonDocument> StateFilter(string? tenant, string? name) =>
        tenant == null || name == null
            ? Builders<BsonDocument>.Filter.Empty
            : GetFilter(tenant, name);
}
