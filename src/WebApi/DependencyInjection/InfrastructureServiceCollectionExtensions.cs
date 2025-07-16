using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Devpro.TerraformBackend.WebApi.DependencyInjection;

internal static class InfrastructureServiceCollectionExtensions
{
    internal static IServiceCollection AddInfrastructure(this IServiceCollection services, ApplicationConfiguration configuration)
    {
        // MongoDB
        services.AddSingleton(configuration.MongoDbConfiguration);
        services.TryAddSingleton<Common.MongoDb.IMongoClientFactory, Common.MongoDb.DefaultMongoClientFactory>();
        services.TryAddScoped<Domain.Repositories.IStateLockRepository, Infrastructure.MongoDb.Repositories.StateLockRepository>();
        services.TryAddScoped<Domain.Repositories.IStateRepository, Infrastructure.MongoDb.Repositories.StateRepository>();
        services.TryAddScoped<Domain.Repositories.IUserRepository, Infrastructure.MongoDb.Repositories.UserRepository>();

        return services;
    }
}
