using System.Threading.Tasks;
using Farseer.TerraformBackend.Domain.Models;

namespace Farseer.TerraformBackend.Domain.Repositories;

public interface IStateLockRepository
{
    Task<StateLockModel?> FindOneAsync(string tenant, string name);

    Task<StateLockModel> CreateAsync(StateLockModel input);

    Task<bool> DeleteAsync(StateLockModel input);
}
