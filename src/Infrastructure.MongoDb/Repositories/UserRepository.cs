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
    /// A valid BCrypt hash, of a value nothing can supply, verified against when the username lookup misses so
    /// that both outcomes cost the same.
    /// <para>
    /// Without it, an unknown username returns as soon as the query does while a known one pays a full BCrypt
    /// verify: measured on this codebase, 3 to 11 ms against 160 to 175 ms. That gap is a reliable
    /// username-enumeration oracle, and it lets an attacker find the valid accounts of every tenant before
    /// spending a single guess on a password.
    /// </para>
    /// <para>
    /// Computed once at startup rather than per call, since generating it costs the same as verifying it.
    /// </para>
    /// <para>
    /// The work factor is pinned rather than left to the library default, and that is the whole point of the
    /// constant. `BCrypt.Net-Next` defaults to 11 while every hash this application stores is written at 10,
    /// by `htpasswd` through `tfbeadm`. Verifying a dummy at 11 costs twice what verifying a real hash costs,
    /// which does not close the oracle: it inverts it, and an unknown username becomes the slow case instead
    /// of the fast one. Measured before pinning: 123 to 184 ms for an unknown username against 58 to 74 ms
    /// for a known one. This must stay in step with the work factor `tfbeadm` produces.
    /// </para>
    /// </summary>
    /// <summary>
    /// The work factor every stored password hash is written at.
    /// Public so that anything creating a user verifies against the same cost, since a user created at a
    /// different work factor reopens the oracle for that account.
    /// </summary>
    public const int StoredHashWorkFactor = 10;

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
