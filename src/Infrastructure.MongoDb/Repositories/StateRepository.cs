using System;
using System.Threading;
using System.Threading.Tasks;
using Devpro.TerraformBackend.Domain.Exceptions;
using Devpro.TerraformBackend.Domain.Repositories;
using Devpro.TerraformBackend.Infrastructure.MongoDb.History;
using Devpro.TerraformBackend.Infrastructure.MongoDb.Serialization;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Devpro.TerraformBackend.Infrastructure.MongoDb.Repositories;

public class StateRepository : RepositoryBase, IStateRepository
{
    /// <summary>
    /// MongoDB's documented maximum BSON document size, used to name the limit in the error.
    /// <c>BsonDefaults.MaxDocumentSize</c> is not used because it is <see cref="int.MaxValue"/> until a connection reports otherwise,
    /// which would name a limit no operator can act on.
    /// </summary>
    private const long MaxDocumentSizeInBytes = 16 * 1024 * 1024;

    private const int BsonObjectTooLargeCode = 10334;

    private static readonly FindOneAndReplaceOptions<BsonDocument> ReplaceReturningPrevious = new()
    {
        IsUpsert = true,
        ReturnDocument = ReturnDocument.Before,
        Projection = Builders<BsonDocument>.Projection.Include("_id").Include("value")
    };

    private readonly IMongoClient _mongoClient;

    private readonly IMongoCollection<BsonDocument> _bsonCollection;

    private readonly StateHistoryQueueRepository _queueRepository;

    private readonly TransactionSupport _transactionSupport;

    private readonly StateHistorySignal _historySignal;

    public StateRepository(IMongoDatabase mongoDatabase, ILogger<StateRepository> logger, StateHistoryQueueRepository queueRepository,
        TransactionSupport transactionSupport, StateHistorySignal historySignal)
        : base(mongoDatabase, logger)
    {
        _mongoClient = mongoDatabase.Client;
        _bsonCollection = GetCollection<BsonDocument>();
        _queueRepository = queueRepository;
        _transactionSupport = transactionSupport;
        _historySignal = historySignal;
    }

    protected override string CollectionName => "tf_state";

    public async Task CreateAsync(string tenant, string name, string jsonInput, CancellationToken cancellationToken = default)
    {
        // parsed before anything else, so that malformed JSON fails as a JsonException here rather than reaching the driver,
        // which is what keeps a bad request distinguishable from an oversized one
        var value = JsonToBsonConverter.Convert(jsonInput);

        var savedAt = DateTime.UtcNow;
        // no _id: a replacement keeps the _id of the document it replaces, and an upsert generates one
        var document = new BsonDocument
        {
            ["tenant"] = tenant,
            ["name"] = name,
            ["created_at"] = new BsonDateTime(savedAt),
            ["value"] = value
        };

        bool queued;
        try
        {
            if (await _transactionSupport.IsSupportedAsync(cancellationToken))
            {
                using var session = await _mongoClient.StartSessionAsync(cancellationToken: cancellationToken);
                queued = await session.WithTransactionAsync(
                    (transaction, token) => WriteAsync(transaction, tenant, name, document, savedAt, token),
                    cancellationToken: cancellationToken);
            }
            else
            {
                queued = await WriteAsync(null, tenant, name, document, savedAt, cancellationToken);
            }
        }
        catch (FormatException exception)
        {
            // the driver refuses to serialise a command above the message limit,
            // and by this point the input has already parsed as JSON, so a format failure here can only be the size
            throw new StateTooLargeException(MaxDocumentSizeInBytes, exception);
        }
        catch (MongoCommandException exception) when (exception.Code == BsonObjectTooLargeCode)
        {
            // the server refuses a replacement above the document limit that still fits in a command
            throw new StateTooLargeException(MaxDocumentSizeInBytes, exception);
        }

        if (queued)
        {
            _historySignal.Notify();
        }
    }

    public async Task<string?> FindOneAsync(string tenant, string name, CancellationToken cancellationToken = default)
    {
        var document = await _bsonCollection.Find(GetFilter(tenant, name))
            .FirstOrDefaultAsync(cancellationToken);
        return document == null ? null : BsonToJsonConverter.Convert(document["value"]);
    }

    /// <summary>
    /// The current document of a state with its <c>_id</c>, which tells the history worker whether it is still the state a queue entry was written for.
    /// </summary>
    public async Task<BsonDocument?> FindDocumentAsync(string tenant, string name, CancellationToken cancellationToken = default) =>
        await _bsonCollection.Find(GetFilter(tenant, name))
            .Project(Builders<BsonDocument>.Projection.Include("_id").Include("value"))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> DeleteAsync(string tenant, string name, CancellationToken cancellationToken = default)
    {
        var deleteResult = await _bsonCollection.DeleteOneAsync(GetFilter(tenant, name), cancellationToken);
        return deleteResult.DeletedCount > 0;
    }

    /// <summary>
    /// Saves the state and queues the change from the version it replaces, returning whether a change was queued.
    /// <para>
    /// Without a session, on a standalone server, the queue entry is written under no cancellation:
    /// once the state is saved, a cancelled request must not leave it without its entry.
    /// </para>
    /// </summary>
    private async Task<bool> WriteAsync(IClientSessionHandle? session, string tenant, string name, BsonDocument document, DateTime savedAt,
        CancellationToken cancellationToken)
    {
        var filter = GetFilter(tenant, name);
        var replaced = session == null
            ? await _bsonCollection.FindOneAndReplaceAsync(filter, document, ReplaceReturningPrevious, cancellationToken)
            : await _bsonCollection.FindOneAndReplaceAsync(session, filter, document, ReplaceReturningPrevious, cancellationToken);

        // a first write has nothing to diff against, and an identical write has no change
        if (replaced == null || replaced["value"].Equals(document["value"])) return false;

        return await _queueRepository.EnqueueAsync(session, replaced["_id"].AsObjectId, tenant, name, savedAt, replaced["value"],
            session == null ? CancellationToken.None : cancellationToken);
    }
}
