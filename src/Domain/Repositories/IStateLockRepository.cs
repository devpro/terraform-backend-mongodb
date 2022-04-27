using System.Collections.Generic;
using System.Threading.Tasks;
using Kalosyni.TerraformBackend.Domain.Models;

namespace Kalosyni.TerraformBackend.Domain.Repositories
{
    public interface IStateLockRepository
    {
        Task<StateLockModel> FindOneAsync(string id);

        Task<List<StateLockModel>> FindAllAsync();

        Task CreateAsync(StateLockModel input);

        Task<long> DeleteAsync(StateLockModel input);
    }
}
