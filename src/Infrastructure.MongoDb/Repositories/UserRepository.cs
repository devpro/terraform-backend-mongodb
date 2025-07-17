using System.Threading.Tasks;
using Farseer.Common.MongoDb;
using Farseer.TerraformBackend.Domain.Models;
using Farseer.TerraformBackend.Domain.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Farseer.TerraformBackend.Infrastructure.MongoDb.Repositories;

public class UserRepository : RepositoryBase, IUserRepository
{
    private readonly IMongoCollection<UserModel> _modelCollection;

    public UserRepository(IMongoClientFactory mongoClientFactory, ILogger<UserRepository> logger, MongoDbConfiguration configuration)
        : base(mongoClientFactory, logger, configuration)
    {
        _modelCollection = GetCollection<UserModel>();
    }

    protected override string CollectionName => "user";

    public async Task<UserModel?> CheckAuthentication(string username, string password)
    {
        var user = await _modelCollection.Find(x => x.Username == username).FirstOrDefaultAsync();
        if (user == null)
        {
            return null;
        }

        return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash) ? user : null;
    }
}
