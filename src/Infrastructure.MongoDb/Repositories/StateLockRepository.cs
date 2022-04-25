using System.Collections.Generic;
using System.Threading.Tasks;
using Kalosyni.Common.MongoDb;
using Kalosyni.TerraformBackend.Domain.Models;
using Kalosyni.TerraformBackend.Domain.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Kalosyni.TerraformBackend.Infrastructure.MongoDb.Repositories
{
    public class StateLockRepository : RepositoryBase, IStateLockRepository
    {
        //private readonly IMongoCollection<BsonDocument> _bsonCollection;

        private readonly IMongoCollection<StateLockModel> _modelCollection;

        public StateLockRepository(IMongoClientFactory mongoClientFactory, ILogger<RepositoryBase> logger, MongoDbConfiguration configuration)
            : base(mongoClientFactory, logger, configuration)
        {
            //_bsonCollection = GetCollection<BsonDocument>();
            _modelCollection = GetCollection<StateLockModel>();
        }

        protected override string CollectionName => "tf_state_lock";

        public async Task<StateLockModel> FindOneAsync(string id)
        {
            return await _modelCollection.Find(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<StateLockModel>> FindAllAsync()
        {
            //var documents = await _bsonCollection.Find(new BsonDocument()).ToListAsync();
            //return documents.Select(x => BsonSerializer.Deserialize<StateLockModel>(x)).ToList();

            return await _modelCollection.Find(_ => true).ToListAsync();
        }

        public async Task CreateAsync(StateLockModel input)
        {
            //TODO: check a lock doesn't exist already
            await _modelCollection.InsertOneAsync(input);
        }

        public async Task<long> DeleteAsync(StateLockModel input)
        {
            var result = await _modelCollection.DeleteOneAsync(x => x.Id == input.Id && x.Name == input.Name);
            return result.DeletedCount;
        }
    }
}
