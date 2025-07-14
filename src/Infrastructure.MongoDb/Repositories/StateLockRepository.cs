using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;
using Devpro.Common.MongoDb;
using Devpro.TerraformBackend.Domain.Models;
using Devpro.TerraformBackend.Domain.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Devpro.TerraformBackend.Infrastructure.MongoDb.Repositories
{
    public class StateLockRepository : RepositoryBase, IStateLockRepository
    {
        //private readonly IMongoCollection<BsonDocument> _bsonCollection;
        private readonly IMongoCollection<StateLockModel> _modelCollection;

        public StateLockRepository(IMongoClientFactory mongoClientFactory, ILogger<StateLockRepository> logger, MongoDbConfiguration configuration)
            : base(mongoClientFactory, logger, configuration)
        {
            //_bsonCollection = GetCollection<BsonDocument>();
            _modelCollection = GetCollection<StateLockModel>();
        }

        protected override string CollectionName => "tf_state_lock";

        public async Task<StateLockModel> FindOneAsync(string name)
        {
            return await _modelCollection.Find(x => x.Name == name).FirstOrDefaultAsync();
        }

        public async Task<List<StateLockModel>> FindAllAsync()
        {
            //var documents = await _bsonCollection.Find(new BsonDocument()).ToListAsync();
            //return documents.Select(x => BsonSerializer.Deserialize<StateLockModel>(x)).ToList();

            return await _modelCollection.Find(_ => true).ToListAsync();
        }

        public async Task<StateLockModel> CreateAsync(StateLockModel input)
        {
            await _modelCollection.InsertOneAsync(input);
            return input;
        }

        public async Task<bool> DeleteAsync(StateLockModel input)
        {
            var result = await _modelCollection.DeleteOneAsync(x => x.Id == input.Id && x.Name == input.Name);
            return result.DeletedCount > 0;
        }
    }
}
