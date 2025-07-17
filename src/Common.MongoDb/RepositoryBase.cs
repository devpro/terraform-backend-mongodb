using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Devpro.Common.MongoDb;

public abstract class RepositoryBase
{
    private readonly IMongoDatabase _mongoDatabase;

    protected RepositoryBase(IMongoClientFactory mongoClientFactory, ILogger<RepositoryBase> logger, MongoDbConfiguration configuration)
    {
        logger.LogDebug("Opening connection to MongoDB");
        MongoClient = mongoClientFactory.CreateClient(configuration.ConnectionString);
        logger.LogDebug("Getting database {DatabaseName}", configuration.DatabaseName);
        _mongoDatabase = MongoClient.GetDatabase(configuration.DatabaseName);
    }

    protected abstract string CollectionName { get; }

    protected MongoClient MongoClient { get; private set; }

    protected IMongoCollection<T> GetCollection<T>()
    {
        return _mongoDatabase.GetCollection<T>(CollectionName);
    }
}
