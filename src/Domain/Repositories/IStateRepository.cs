using System.Threading.Tasks;

namespace Kalosyni.TerraformBackend.Domain.Repositories
{
    public interface IStateRepository
    {
        Task<string> FindOneAsync(string name);

        Task CreateAsync(string name, string jsonInput);
    }
}
