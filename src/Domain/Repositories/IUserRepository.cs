using System.Threading.Tasks;
using Devpro.TerraformBackend.Domain.Models;

namespace Devpro.TerraformBackend.Domain.Repositories;

public interface IUserRepository
{
    Task<UserModel?> CheckAuthentication(string username, string password);
}
