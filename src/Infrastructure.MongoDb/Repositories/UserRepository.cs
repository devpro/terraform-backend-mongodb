using System.Threading.Tasks;
using Devpro.TerraformBackend.Domain.Models;
using Devpro.TerraformBackend.Domain.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Devpro.TerraformBackend.Infrastructure.MongoDb.Repositories;

public class UserRepository : RepositoryBase, IUserRepository
{
    private readonly IMongoCollection<UserModel> _modelCollection;

    public UserRepository(IMongoDatabase mongoDatabase, ILogger<UserRepository> logger)
        : base(mongoDatabase, logger)
    {
        _modelCollection = GetCollection<UserModel>();
    }

    protected override string CollectionName => "user";

    public async Task<UserModel?> CheckAuthentication(string username, string password)
    {
        if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("Checking authentication");

        var user = await _modelCollection.Find(x => x.Username == username).FirstOrDefaultAsync();
        if (user == null)
        {
            if (Logger.IsEnabled(LogLevel.Information)) Logger.LogInformation("Authentication failed. Username {username} doesn't exist", username);
            return null;
        }

        if (BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return user;
        }

        if (Logger.IsEnabled(LogLevel.Information)) Logger.LogInformation("Authentication failed. Password for username {username} is incorrect", username);
        return null;
    }
}
