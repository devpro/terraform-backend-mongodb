using System.Threading;
using System.Threading.Tasks;
using Devpro.TerraformBackend.Domain.Models;
using Devpro.TerraformBackend.Domain.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Devpro.TerraformBackend.Infrastructure.MongoDb.Repositories;

public class StateLockRepository : RepositoryBase, IStateLockRepository
{
    private readonly IMongoCollection<StateLockModel> _modelCollection;

    public StateLockRepository(IMongoDatabase mongoDatabase, ILogger<StateLockRepository> logger)
        : base(mongoDatabase, logger)
    {
        _modelCollection = GetCollection<StateLockModel>();
    }

    protected override string CollectionName => "tf_state_lock";

    public async Task<StateLockModel?> FindOneAsync(string tenant, string name, CancellationToken cancellationToken = default)
    {
        return await _modelCollection.Find(x => x.Tenant == tenant && x.Name == name)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<StateLockModel?> CreateAsync(StateLockModel input, CancellationToken cancellationToken = default)
    {
        try
        {
            await _modelCollection.InsertOneAsync(input, cancellationToken: cancellationToken);
            return input;
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            if (Logger.IsEnabled(LogLevel.Information)) Logger.LogInformation("Lock already exists for tenant {Tenant} and state {Name}", input.Tenant, input.Name);
            return null;
        }
    }

    public async Task<bool> DeleteAsync(StateLockModel input, CancellationToken cancellationToken = default)
    {
        var result = await _modelCollection.DeleteOneAsync(x =>
            x.Tenant == input.Tenant
            && x.Name == input.Name
            && x.Id == input.Id, cancellationToken);
        return result.DeletedCount > 0;
    }
}
