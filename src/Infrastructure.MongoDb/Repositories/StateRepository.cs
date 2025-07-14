using System;
using System.Threading.Tasks;
using Devpro.Common.MongoDb;
using Devpro.TerraformBackend.Domain.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Devpro.TerraformBackend.Infrastructure.MongoDb.Repositories
{
    public class StateRepository(IMongoClientFactory mongoClientFactory, ILogger<StateRepository> logger, MongoDbConfiguration configuration)
        : RepositoryBase(mongoClientFactory, logger, configuration), IStateRepository
    {
        protected override string CollectionName => "tf_state";

        public async Task CreateAsync(string name, string jsonInput)
        {
            //TODO: makes it the latest value
            var document = new BsonDocument
            {
                ["_id"] = new BsonObjectId(ObjectId.GenerateNewId()),
                ["name"] = name,
                ["createdAt"] = new BsonDateTime(DateTime.UtcNow), // stored as ToString("yyyy-MM-ddTHH:mm:ss.fff+00:00")
                ["value"] = BsonDocument.Parse(jsonInput)
            };
            var collection = GetCollection<BsonDocument>();
            await collection.InsertOneAsync(document);
        }

        public async Task<string> FindOneAsync(string name)
        {
            var collection = GetCollection<BsonDocument>();
            var document = await collection.Find(new BsonDocument("name", name))
                .Sort(Builders<BsonDocument>.Sort.Descending("createdAt"))
                .FirstOrDefaultAsync();
            if (document == null)
            {
                return string.Empty;
            }

            return document["value"].ToJson();
        }
    }
}
