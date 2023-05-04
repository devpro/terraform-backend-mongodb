using Devpro.Common.MongoDb;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Devpro.TerraformBackend.Infrastructure.MongoDb.Repositories
{
    public abstract class RepositoryBase
    {
        private readonly IMongoClientFactory _mongoClientFactory;

        private readonly ILogger<RepositoryBase> _logger;

        private readonly MongoDbConfiguration _configuration;

        private readonly MongoClient _mongoClient;

        private readonly IMongoDatabase _mongoDatabase;

        protected RepositoryBase(IMongoClientFactory mongoClientFactory, ILogger<RepositoryBase> logger, MongoDbConfiguration configuration)
        {
            _mongoClientFactory = mongoClientFactory;
            _logger = logger;
            _configuration = configuration;

            _logger.LogDebug("Opening connection to MongoDB");
            _mongoClient = _mongoClientFactory.CreateClient(_configuration.ConnectionString);
            _logger.LogDebug("Getting database {DatabaseName}", _configuration.DatabaseName);
            _mongoDatabase = _mongoClient.GetDatabase(_configuration.DatabaseName);
        }

        protected abstract string CollectionName { get; }

        protected IMongoCollection<T> GetCollection<T>()
        {
            return _mongoDatabase.GetCollection<T>(CollectionName);
        }
    }
}
