using System;
using System.Linq;
using System.Threading.Tasks;
using Kalosyni.Common.MongoDb;
using Kalosyni.TerraformBackend.Domain.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Kalosyni.TerraformBackend.Infrastructure.MongoDb.Repositories
{
    public class StateRepository : RepositoryBase, IStateRepository
    {
        public StateRepository(IMongoClientFactory mongoClientFactory, ILogger<StateRepository> logger, MongoDbConfiguration configuration)
            : base(mongoClientFactory, logger, configuration)
        {
        }

        protected override string CollectionName => "tf_state";

        public async Task CreateAsync(string name, string jsonInput)
        {
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
            var documents = await collection.Find(new BsonDocument()).ToListAsync();
            return documents.Select(x => x["value"].ToJson()).FirstOrDefault();
        }
    }
}
