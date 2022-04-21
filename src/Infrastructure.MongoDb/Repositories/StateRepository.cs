using System.Collections.Generic;
using System.Threading.Tasks;
using Kalosyni.TerraformBackend.Domain.Models;
using Kalosyni.TerraformBackend.Domain.Repositories;

namespace Kalosyni.TerraformBackend.Infrastructure.MongoDb.Repositories
{
    public class StateRepository : IStateRepository
    {
        public Task<List<StateModel>> GetAllAsync()
        {
            return Task.FromResult(new List<StateModel>());
        }
    }
}
