using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kalosyni.Common.MongoDb;
using Kalosyni.TerraformBackend.Domain.Models;
using Kalosyni.TerraformBackend.Domain.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Kalosyni.TerraformBackend.Infrastructure.MongoDb.Repositories
{
    public class StateLockRepository : RepositoryBase, IStateLockRepository
    {
        public StateLockRepository(IMongoClientFactory mongoClientFactory, ILogger<RepositoryBase> logger, MongoDbConfiguration configuration)
            : base(mongoClientFactory, logger, configuration)
        {
        }

        protected override string CollectionName => "tf_state_lock";

        public async Task<List<StateLockModel>> GetAllAsync()
        {
            var collection = GetCollection<BsonDocument>();
            var documents = await collection.Find(new BsonDocument()).ToListAsync();
            return documents.Select(x => BsonSerializer.Deserialize<StateLockModel>(x)).ToList();
        }

        public async Task CreateAsync(StateLockModel input)
        {
            var document = input.ToBsonDocument();
            document["Id"] = new BsonObjectId(ObjectId.GenerateNewId());
            var collection = GetCollection<BsonDocument>();
            await collection.InsertOneAsync(document);
        }

        public Task DeleteAsync(StateLockModel creationModel)
        {
            throw new System.NotImplementedException();
        }
    }
}
