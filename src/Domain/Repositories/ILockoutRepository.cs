using System;
using System.Threading.Tasks;
using Devpro.TerraformBackend.Domain.Models;

namespace Devpro.TerraformBackend.Domain.Repositories;

public interface ILockoutRepository
{
    /// <summary>
    /// The current failure count for a username and source address pair.
    /// Zero both when nothing was ever recorded and when the last recorded window has expired.
    /// </summary>
    Task<int> GetFailureCountAsync(string username, string remoteAddress);

    /// <summary>
    /// The failure count and the current user in one database request, for a credential whose BCrypt verify is already cached.
    /// <para>
    /// The user is read on every cached authentication, so a change of tenant, a deletion or a new password applies at the next request.
    /// Read together with the counter, it costs no more requests than the counter alone.
    /// </para>
    /// </summary>
    Task<(int Failures, UserModel? User)> GetFailureCountAndUserAsync(string username, string remoteAddress);

    /// <summary>
    /// Atomically records one failure and returns the count after it.
    /// Extends the counter of a still-open window; starts a fresh window, expiring after <paramref name="lockoutDuration"/>, if none was open.
    /// </summary>
    Task<int> RecordFailureAsync(string username, string remoteAddress, TimeSpan lockoutDuration);

    /// <summary>Clears the counter, called after a successful authentication.</summary>
    Task ClearAsync(string username, string remoteAddress);
}
