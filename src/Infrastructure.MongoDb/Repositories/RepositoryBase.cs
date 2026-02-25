using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Devpro.TerraformBackend.Infrastructure.MongoDb.Repositories;

public abstract class RepositoryBase(IMongoDatabase mongoDatabase, ILogger<RepositoryBase> logger)
{
    protected abstract string CollectionName { get; }

    protected IMongoCollection<T> GetCollection<T>()
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Getting collection of type {Name}", typeof(T).Name);
        }

        return mongoDatabase.GetCollection<T>(CollectionName);
    }

    protected static BsonDocument GetFilter(string tenant, string name)
    {
        return new BsonDocument
        {
            { "tenant", tenant },
            { "name", name }
        };
    }
}
