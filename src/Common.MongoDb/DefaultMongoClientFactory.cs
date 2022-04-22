using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace Kalosyni.Common.MongoDb
{
    public class DefaultMongoClientFactory : IMongoClientFactory
    {
        public DefaultMongoClientFactory()
        {
            RegisterConventions();
        }

        public MongoClient CreateClient(string connectionStringName)
        {
            return new MongoClient(connectionStringName);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// - See https://github.com/mongodb/mongo-csharp-driver/tree/master/src/MongoDB.Bson/Serialization/Conventions
        /// </remarks>
        private void RegisterConventions()
        {
            var pack = new ConventionPack
            {
                new CamelCaseElementNameConvention(),
                new EnumRepresentationConvention(BsonType.String),
                new IgnoreExtraElementsConvention(true),
                new IgnoreIfNullConvention(true)
            };
            ConventionRegistry.Register("Conventions", pack, t => true);
        }
    }
}
