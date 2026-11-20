using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Devpro.TerraformBackend.Infrastructure.MongoDb.Repositories;

public class StateHistoryRepository : RepositoryBase
{
    private readonly IMongoCollection<BsonDocument> _bsonCollection;

    public StateHistoryRepository(IMongoDatabase mongoDatabase, ILogger<StateHistoryRepository> logger)
        : base(mongoDatabase, logger)
    {
        _bsonCollection = GetCollection<BsonDocument>();
    }

    protected override string CollectionName => "tf_state_history";

    /// <summary>
    /// Writes the change of one state write, under the <c>_id</c> of its queue entry.
    /// <para>
    /// Reusing that <c>_id</c> makes the write idempotent: a worker that stops after this write and before removing the queue entry
    /// computes the entry again on the next claim, and the second insert is refused rather than duplicated.
    /// </para>
    /// </summary>
    public async Task CreateAsync(ObjectId id, string tenant, string name, DateTime createdAt, JsonNode patch, CancellationToken cancellationToken = default)
    {
        var document = new BsonDocument
        {
            ["_id"] = id,
            ["tenant"] = tenant,
            ["name"] = name,
            ["created_at"] = new BsonDateTime(createdAt),
            ["upgrade"] = patch.ToJsonString()
        };

        try
        {
            await _bsonCollection.InsertOneAsync(document, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // written by an earlier attempt at the same entry
        }
    }
}
