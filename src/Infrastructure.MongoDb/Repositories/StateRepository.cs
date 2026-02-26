using System;
using System.Text.Json.JsonDiffPatch;
using System.Text.Json.JsonDiffPatch.Diffs.Formatters;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Devpro.TerraformBackend.Domain.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Devpro.TerraformBackend.Infrastructure.MongoDb.Repositories;

public class StateRepository : RepositoryBase, IStateRepository
{
    private readonly StateHistoryRepository _stateHistoryRepository;

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
        var filter = GetFilter(tenant, name);
        var existing = await _bsonCollection.Find(filter)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            var diffNode = GenerateJsonDiff(existing["value"].ToJson(), jsonInput);
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
            ["value"] = BsonDocument.Parse(jsonInput)
        };
        await _bsonCollection.ReplaceOneAsync(filter, document, new ReplaceOptions { IsUpsert = true });
    }

    public async Task<string?> FindOneAsync(string tenant, string name)
    {
        var document = await _bsonCollection.Find(GetFilter(tenant, name))
            .FirstOrDefaultAsync();
        return document?["value"].ToJson();
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
