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

    /// <summary>
    /// The work factor every stored password hash is written at, by <c>htpasswd</c> through <c>tfbeadm</c>.
    /// <para>
    /// Pinned rather than left to the library default of 11, because the dummy hash below must cost exactly what a real one costs: a dummy at 11 takes twice as long, which inverts the enumeration oracle rather than closing it.
    /// Public so that anything creating a user writes at the same cost, and must stay in step with <c>tfbeadm</c>, since an account at a different cost reopens the oracle for that account.
    /// </para>
    /// </summary>
    public const int StoredHashWorkFactor = 10;

    /// <summary>
    /// A valid hash of a value nothing can supply, verified when the username lookup misses, so that an unknown username costs the same BCrypt verify as a known one.
    /// Without it the response time tells an attacker which usernames exist.
    /// </summary>
    private static readonly string DummyPasswordHash =
        BCrypt.Net.BCrypt.HashPassword("this value is never a password", StoredHashWorkFactor);

    protected override string CollectionName => "user";

    public async Task<UserModel?> CheckAuthentication(string username, string password)
    {
        if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("Checking authentication");

        var user = await _modelCollection.Find(x => x.Username == username).FirstOrDefaultAsync();
        if (user == null)
        {
            BCrypt.Net.BCrypt.Verify(password, DummyPasswordHash);
            return null;
        }

        return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash) ? user : null;
    }
}
