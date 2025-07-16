using System.Threading.Tasks;

namespace Devpro.TerraformBackend.Domain.Repositories;

public interface IStateRepository
{
    Task<string?> FindOneAsync(string tenant, string name);

    Task CreateAsync(string tenant, string name, string jsonInput);

    Task<bool> DeleteAsync(string tenant, string name);
}
