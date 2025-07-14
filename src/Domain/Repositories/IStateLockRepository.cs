using System.Collections.Generic;
using System.Threading.Tasks;
using Devpro.TerraformBackend.Domain.Models;

namespace Devpro.TerraformBackend.Domain.Repositories;

public interface IStateLockRepository
{
    Task<StateLockModel> FindOneAsync(string name);

    Task<StateLockModel> CreateAsync(StateLockModel input);

    Task<bool> DeleteAsync(StateLockModel input);
}
