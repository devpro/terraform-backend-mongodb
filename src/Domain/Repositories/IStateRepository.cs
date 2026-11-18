using System.Threading;
using System.Threading.Tasks;

namespace Devpro.TerraformBackend.Domain.Repositories;

public interface IStateRepository
{
    Task<string?> FindOneAsync(string tenant, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the state, and queues the change from the version it replaces for the history, which is computed after the write returns.
    /// On a server that runs transactions, the state and its queue entry are written together.
    /// </summary>
    Task CreateAsync(string tenant, string name, string jsonInput, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string tenant, string name, CancellationToken cancellationToken = default);
}
