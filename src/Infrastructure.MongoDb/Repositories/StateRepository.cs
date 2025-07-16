using System;
using System.Threading.Tasks;
using Devpro.Common.MongoDb;
using Devpro.TerraformBackend.Domain.Models;
using Devpro.TerraformBackend.Domain.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Devpro.TerraformBackend.Infrastructure.MongoDb.Repositories;

public class StateRepository : RepositoryBase, IStateRepository
{
    private readonly IMongoCollection<BsonDocument> _bsonCollection;

    public StateRepository(IMongoClientFactory mongoClientFactory, ILogger<StateLockRepository> logger, MongoDbConfiguration configuration)
        : base(mongoClientFactory, logger, configuration)
    {
        _bsonCollection = GetCollection<BsonDocument>();
    }

    protected override string CollectionName => "tf_state";

    public async Task CreateAsync(string tenant, string name, string jsonInput)
    {
        var document = new BsonDocument
        {
            ["_id"] = new BsonObjectId(ObjectId.GenerateNewId()),
            ["tenant"] = tenant,
            ["name"] = name,
            ["createdAt"] = new BsonDateTime(DateTime.UtcNow), // stored as ToString("yyyy-MM-ddTHH:mm:ss.fff+00:00")
            ["value"] = BsonDocument.Parse(jsonInput)
        };
        await _bsonCollection.InsertOneAsync(document);
    }

    public async Task<string?> FindOneAsync(string tenant, string name)
    {
        var document = await _bsonCollection.Find(GetFilter(tenant, name))
            .Sort(Builders<BsonDocument>.Sort.Descending("createdAt"))
            .FirstOrDefaultAsync();
        if (document == null)
        {
            return null;
        }

        return document["value"].ToJson();
    }

    public async Task<bool> DeleteAsync(string tenant, string name)
    {
        var deleteResult = await _bsonCollection.DeleteOneAsync(GetFilter(tenant, name));
        return deleteResult.DeletedCount > 0;
    }

    private static BsonDocument GetFilter(string tenant, string name)
    {
        return new BsonDocument
        {
            { "tenant", tenant },
            { "name", name }
        };
    }
}
