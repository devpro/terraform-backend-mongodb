using System.Collections.Generic;
using System.Threading.Tasks;
using Devpro.TerraformBackend.Domain.Models;

namespace Devpro.TerraformBackend.Domain.Repositories;

public interface IStateLockRepository
{
    Task<StateLockModel> FindOneAsync(string id);

    Task<List<StateLockModel>> FindAllAsync();

    Task CreateAsync(StateLockModel input);

    Task<long> DeleteAsync(StateLockModel input);
}
