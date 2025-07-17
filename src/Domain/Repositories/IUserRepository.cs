using System.Threading.Tasks;
using Farseer.TerraformBackend.Domain.Models;

namespace Farseer.TerraformBackend.Domain.Repositories;

public interface IUserRepository
{
    Task<UserModel?> CheckAuthentication(string username, string password);
}
