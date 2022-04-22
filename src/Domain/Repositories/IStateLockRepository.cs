using System.Collections.Generic;
using System.Threading.Tasks;
using Kalosyni.TerraformBackend.Domain.Models;

namespace Kalosyni.TerraformBackend.Domain.Repositories
{
    public interface IStateLockRepository
    {
        Task<List<StateLockModel>> GetAllAsync();

        Task CreateAsync(StateLockModel creationModel);

        Task DeleteAsync(StateLockModel creationModel);
    }
}
