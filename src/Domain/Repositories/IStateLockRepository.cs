using System.Threading;
using System.Threading.Tasks;
using Devpro.TerraformBackend.Domain.Models;

namespace Devpro.TerraformBackend.Domain.Repositories;

public interface IStateLockRepository
{
    Task<StateLockModel?> FindOneAsync(string tenant, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the lock atomically.
    /// Returns null when a lock already exists for the same tenant and state name.
    /// </summary>
    Task<StateLockModel?> CreateAsync(StateLockModel input, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(StateLockModel input, CancellationToken cancellationToken = default);
}
