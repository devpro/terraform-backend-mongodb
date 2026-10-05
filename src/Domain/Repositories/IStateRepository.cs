using System.Threading;
using System.Threading.Tasks;

namespace Devpro.TerraformBackend.Domain.Repositories;

public interface IStateRepository
{
    Task<string?> FindOneAsync(string tenant, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the state, recording the change from the previous version in the history.
    /// The token cancels only the read that precedes the writes, since the history write and the state write are not atomic and a cancellation between them would leave a history entry with no matching state.
    /// </summary>
    Task CreateAsync(string tenant, string name, string jsonInput, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string tenant, string name, CancellationToken cancellationToken = default);
}
