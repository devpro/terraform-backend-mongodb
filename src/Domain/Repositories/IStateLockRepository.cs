using System.Threading.Tasks;
using Devpro.TerraformBackend.Domain.Models;

namespace Devpro.TerraformBackend.Domain.Repositories;

public interface IStateLockRepository
{
    Task<StateLockModel?> FindOneAsync(string tenant, string name);

    Task<StateLockModel> CreateAsync(StateLockModel input);

    Task<bool> DeleteAsync(StateLockModel input);
}
