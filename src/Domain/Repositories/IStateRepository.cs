using System.Collections.Generic;
using System.Threading.Tasks;
using Kalosyni.TerraformBackend.Domain.Models;

namespace Kalosyni.TerraformBackend.Domain.Repositories
{
    public interface IStateRepository
    {
        public Task<List<StateModel>> GetAllAsync();
    }
}
