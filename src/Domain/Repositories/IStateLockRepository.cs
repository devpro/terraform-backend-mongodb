using System.Threading.Tasks;
using Devpro.TerraformBackend.Domain.Models;

namespace Devpro.TerraformBackend.Domain.Repositories;

public interface IStateLockRepository
{
    Task<StateLockModel?> FindOneAsync(string tenant, string name);

    /// <summary>
    /// Creates the lock atomically.
    /// Returns null when a lock already exists for the same tenant and state name.
    /// </summary>
    Task<StateLockModel?> CreateAsync(StateLockModel input);

    Task<bool> DeleteAsync(StateLockModel input);
}
