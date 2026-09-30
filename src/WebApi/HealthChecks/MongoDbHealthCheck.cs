using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Devpro.TerraformBackend.WebApi.HealthChecks;

/// <summary>
/// Health check reporting whether the MongoDB database answers a ping command.
/// </summary>
public class MongoDbHealthCheck(IMongoDatabase mongoDatabase) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await mongoDatabase.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy("MongoDB ping succeeded");
        }
        catch (Exception exception)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "MongoDB ping failed", exception);
        }
    }
}
