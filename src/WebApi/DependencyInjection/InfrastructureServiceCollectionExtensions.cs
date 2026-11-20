using Devpro.TerraformBackend.Infrastructure.MongoDb.History;
using Devpro.TerraformBackend.Infrastructure.MongoDb.Repositories;
using Devpro.TerraformBackend.WebApi.History;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace Devpro.TerraformBackend.WebApi.DependencyInjection;

internal static class InfrastructureServiceCollectionExtensions
{
    internal static void AddMongoDbInfrastructure(this IServiceCollection services, ApplicationConfiguration configuration)
    {
        if (configuration.HistoryMaxDiffComparisons < 1)
        {
            // below 1, no change that holds an array would ever reach the history
            throw new InvalidOperationException(
                $"History:MaxDiffComparisons must be at least 1, but is {configuration.HistoryMaxDiffComparisons}.");
        }

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

        services.TryAddSingleton<TransactionSupport>();
        services.TryAddSingleton<StateHistorySignal>();
        services.TryAddSingleton(new StateHistoryOptions { MaxDiffComparisons = configuration.HistoryMaxDiffComparisons });

        services.TryAddScoped<Domain.Repositories.IStateLockRepository, StateLockRepository>();
        services.TryAddScoped<StateRepository>();
        services.TryAddScoped<Domain.Repositories.IStateRepository>(sp => sp.GetRequiredService<StateRepository>());
        services.TryAddScoped<Domain.Repositories.IUserRepository, UserRepository>();
        services.TryAddScoped<Domain.Repositories.ILockoutRepository, LockoutRepository>();
        services.TryAddScoped<StateHistoryRepository>();
        services.TryAddScoped<StateHistoryQueueRepository>();
        services.TryAddScoped<StateHistoryProcessor>();

        if (configuration.IsHistoryWorkerEnabled)
        {
            services.AddHostedService<StateHistoryWorker>();
        }
    }
}
