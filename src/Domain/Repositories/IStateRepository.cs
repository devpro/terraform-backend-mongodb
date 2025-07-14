using System.Threading.Tasks;

namespace Devpro.TerraformBackend.Domain.Repositories;

public interface IStateRepository
{
    Task<string> FindOneAsync(string name);

    Task CreateAsync(string name, string jsonInput);

    Task<bool> DeleteAsync(string name);
}
