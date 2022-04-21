namespace Kalosyni.TerraformBackend.WebApi.DependencyInjection
{
    internal static class InfrastructureExtensions
    {
        internal static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            // MongoDB
            services.AddTransient<Domain.Repositories.IStateRepository, Infrastructure.MongoDb.Repositories.StateRepository>();

            return services;
        }
    }
}
