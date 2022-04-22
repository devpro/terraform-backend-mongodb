using MongoDB.Driver;

namespace Kalosyni.Common.MongoDb
{
    public interface IMongoClientFactory
    {
        MongoClient CreateClient(string connectionStringName);
    }
}
