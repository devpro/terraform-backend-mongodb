using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace Devpro.TerraformBackend.WebApi.DependencyInjection;

internal static class InfrastructureServiceCollectionExtensions
{
    internal static void AddMongoDbInfrastructure(this IServiceCollection services, ApplicationConfiguration configuration)
    {
        services.AddSingleton<IMongoClient>(sp =>
        {
            var pack = new ConventionPack
            {
                new CamelCaseElementNameConvention(),
                new EnumRepresentationConvention(BsonType.String),
                new IgnoreExtraElementsConvention(true),
                new IgnoreIfNullConvention(true)
            };
            ConventionRegistry.Register("Conventions", pack, t => true);
            return new MongoClient(configuration.ConnectionString);
        });

        services.AddSingleton<IMongoDatabase>(sp =>
            sp.GetRequiredService<IMongoClient>().GetDatabase(configuration.DatabaseName));

        services.TryAddScoped<Domain.Repositories.IStateLockRepository, Infrastructure.MongoDb.Repositories.StateLockRepository>();
        services.TryAddScoped<Domain.Repositories.IStateRepository, Infrastructure.MongoDb.Repositories.StateRepository>();
        services.TryAddScoped<Domain.Repositories.IUserRepository, Infrastructure.MongoDb.Repositories.UserRepository>();
        services.TryAddScoped<Infrastructure.MongoDb.Repositories.StateHistoryRepository>();
    }
}
