using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kalosyni.TerraformBackend.WebApi.DependencyInjection
{
    internal static class InfrastructureExtensions
    {
        internal static IServiceCollection AddInfrastructure(this IServiceCollection services, ApplicationConfiguration configuration)
        {
            // MongoDB
            services.AddSingleton(configuration.MongoDbConfiguration);
            services.TryAddScoped<Common.MongoDb.IMongoClientFactory, Common.MongoDb.DefaultMongoClientFactory>();
            services.AddTransient<Domain.Repositories.IStateLockRepository, Infrastructure.MongoDb.Repositories.StateLockRepository>();
            services.AddTransient<Domain.Repositories.IStateRepository, Infrastructure.MongoDb.Repositories.StateRepository>();

            return services;
        }
    }
}
