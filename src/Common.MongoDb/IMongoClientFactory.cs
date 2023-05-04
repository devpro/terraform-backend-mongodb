using MongoDB.Driver;

namespace Devpro.Common.MongoDb
{
    /// <summary>
    /// Avoids calling "new" in application code.
    /// </summary>
    public interface IMongoClientFactory
    {
        /// <summary>
        /// Creates MongoDB client from a given connection string.
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        MongoClient CreateClient(string connectionString);
    }
}
