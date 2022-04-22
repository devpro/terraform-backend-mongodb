using Kalosyni.Common.MongoDb;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Kalosyni.TerraformBackend.Infrastructure.MongoDb.Repositories
{
    public abstract class RepositoryBase
    {
        private readonly IMongoClientFactory _mongoClientFactory;

        private readonly ILogger<RepositoryBase> _logger;

        private readonly MongoDbConfiguration _configuration;

        protected RepositoryBase(IMongoClientFactory mongoClientFactory, ILogger<RepositoryBase> logger, MongoDbConfiguration configuration)
        {
            _mongoClientFactory = mongoClientFactory;
            _logger = logger;
            _configuration = configuration;
        }

        protected abstract string CollectionName { get; }

        protected IMongoCollection<T> GetCollection<T>()
        {
            var mongoDbClient = _mongoClientFactory.CreateClient(_configuration.ConnectionString);
            return mongoDbClient.GetDatabase(_configuration.DatabaseName).GetCollection<T>(CollectionName);
        }
    }
}
