using System;
using System.Text.Json.Nodes;
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

    public async Task CreateAsync(string tenant, string name, JsonNode patch)
    {
        var document = new BsonDocument
        {
            ["_id"] = new BsonObjectId(ObjectId.GenerateNewId()),
            ["tenant"] = tenant,
            ["name"] = name,
            ["createdAt"] = new BsonDateTime(DateTime.UtcNow),
            ["patch"] = patch.ToJsonString()
        };
        await _bsonCollection.InsertOneAsync(document);
    }
}
