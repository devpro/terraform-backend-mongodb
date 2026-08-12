using System;
using System.Text.Json.JsonDiffPatch;
using System.Text.Json.JsonDiffPatch.Diffs.Formatters;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Devpro.TerraformBackend.Domain.Exceptions;
using Devpro.TerraformBackend.Domain.Repositories;
using Devpro.TerraformBackend.Infrastructure.MongoDb.Serialization;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Devpro.TerraformBackend.Infrastructure.MongoDb.Repositories;

public class StateRepository : RepositoryBase, IStateRepository
{
    private readonly StateHistoryRepository _stateHistoryRepository;

    /// <summary>
    /// MongoDB's maximum BSON document size.
    /// <para>
    /// Taken from the server's documented limit rather than from <c>BsonDefaults.MaxDocumentSize</c>, which is
    /// <see cref="int.MaxValue"/> until a connection reports otherwise and would name a limit no operator can
    /// act on. The driver's own ceiling is slightly higher, since it allows for command overhead, so a state
    /// refused here is one MongoDB would refuse anyway.
    /// </para>
    /// </summary>
    private const long MaxDocumentSizeInBytes = 16 * 1024 * 1024;

    private readonly IMongoCollection<BsonDocument> _bsonCollection;

    public StateRepository(IMongoDatabase mongoDatabase, ILogger<StateRepository> logger, StateHistoryRepository stateHistoryRepository)
        : base(mongoDatabase, logger)
    {
        _stateHistoryRepository = stateHistoryRepository;
        _bsonCollection = GetCollection<BsonDocument>();
    }

    protected override string CollectionName => "tf_state";

    public async Task CreateAsync(string tenant, string name, string jsonInput)
    {
        // parsed before anything else, so that malformed JSON fails as a JsonException here rather than
        // reaching the driver, which is what keeps a bad request distinguishable from an oversized one
        var value = JsonToBsonConverter.Convert(jsonInput);

        var filter = GetFilter(tenant, name);
        var existing = await _bsonCollection.Find(filter)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            var diffNode = GenerateJsonDiff(BsonToJsonConverter.Convert(existing["value"]), jsonInput);
            if (diffNode != null)
            {
                await _stateHistoryRepository.CreateAsync(tenant, name, diffNode);
            }
        }

        var document = new BsonDocument
        {
            ["_id"] = existing == null ? new BsonObjectId(ObjectId.GenerateNewId()) : existing["_id"].AsObjectId,
            ["tenant"] = tenant,
            ["name"] = name,
            ["createdAt"] = new BsonDateTime(DateTime.UtcNow),
            ["value"] = value
        };

        try
        {
            await _bsonCollection.ReplaceOneAsync(filter, document, new ReplaceOptions { IsUpsert = true });
        }
        catch (FormatException exception)
        {
            // the driver reports an oversized document by refusing to serialise it, and by this point the
            // input has already parsed as JSON, so a format failure here can only be the size
            throw new StateTooLargeException(MaxDocumentSizeInBytes, exception);
        }
    }

    public async Task<string?> FindOneAsync(string tenant, string name)
    {
        var document = await _bsonCollection.Find(GetFilter(tenant, name))
            .FirstOrDefaultAsync();
        return document == null ? null : BsonToJsonConverter.Convert(document["value"]);
    }

    public async Task<bool> DeleteAsync(string tenant, string name)
    {
        var deleteResult = await _bsonCollection.DeleteOneAsync(GetFilter(tenant, name));
        return deleteResult.DeletedCount > 0;
    }

    private static JsonNode? GenerateJsonDiff(string first, string second)
    {
        var diffNode = JsonDiffPatcher.Diff(first, second, new JsonPatchDeltaFormatter());
        return diffNode switch
        {
            null or JsonObject { Count: 0 } or JsonArray { Count: 0 } => null,
            _ => diffNode
        };
    }
}
